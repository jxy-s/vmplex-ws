﻿/*
 * Copyright (c) 2022 Ira Strawser. All rights reserved.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Threading;

using EasyWMI;
using HyperV;

namespace VMPlex
{
    class VMManager
    {
        //private static WMIHelper helper = new WMIHelper(Namespace);
        private static WmiScope scope;

        private static string GuidSelector(string guid) => "SELECT * FROM Msvm_ComputerSystem WHERE Name='" + guid + "'";
        private static string NameSelector(string name) => "SELECT * FROM Msvm_ComputerSystem WHERE ElementName='" + name + "'";

        public static IMsvm_ComputerSystem GetVM(string name) =>
            scope.QueryInstances<IMsvm_ComputerSystem>(NameSelector(name)).FirstOrDefault();

        public static IMsvm_ComputerSystem GetVMByGuid(string guid) =>
            scope.QueryInstances<IMsvm_ComputerSystem>(GuidSelector(guid)).FirstOrDefault();

        public static WmiSubscription<IMsvm_ComputerSystem> CreateMsvmWatcher(string guid) =>
            scope.Subscribe<IMsvm_ComputerSystem>("SELECT * FROM __InstanceModificationEvent WITHIN 1 WHERE TargetInstance ISA 'Msvm_ComputerSystem' AND TargetInstance.Name = '" + guid + "'");

        // Implement singleton
        private static readonly Lazy<VMManager> lazy = new Lazy<VMManager>(() => new VMManager());
        public static VMManager Instance { get { return lazy.Value; } }

        private VMManager()
        {
            scope = new WmiScope(@"root\virtualization\v2");

            vsms = scope.GetInstance<IMsvm_VirtualSystemManagementService>();
            if (vsms == null)
            {
                UI.MessageBox.Show(
                   System.Windows.MessageBoxImage.Error,
                    "Vrtual System Management",
                    "VMPlex is unable to interact with the Virtual Machine Management Service. Please run as administrator or add your user to the Hyper-V Administrators group.");
                Environment.Exit(0xdead);
            }

            creationWatcher = scope.Subscribe<IMsvm_ComputerSystem>("__InstanceCreationEvent", 1);
            modificationWatcher = scope.Subscribe<IMsvm_ComputerSystem>("__InstanceModificationEvent", 1);
            deletionWatcher = scope.Subscribe<IMsvm_ComputerSystem>("__InstanceDeletionEvent", 1);

            // Initialize list of VMs
            VirtualMachines = new ObservableCollection<VirtualMachine>(GetVMs());
            BindingOperations.EnableCollectionSynchronization(VirtualMachines, vmListLock);
            UpdateSummaryInformation();

            creationWatcher.EventArrived += OnCreateInstance;
            deletionWatcher.EventArrived += OnDeleteInstance;
            modificationWatcher.EventArrived += OnModifyInstance;

            new Thread(() => UpdateDataThread()) { IsBackground = true }.Start();
        }

        void UpdateDataThread()
        {
            while (true)
            {
                UpdateSummaryInformation();
                Thread.Sleep(1000);
            }
        }

        // singleton funcs
        private void OnCreateInstance(object sender, WmiEvent<IMsvm_ComputerSystem> e)
        {
            IMsvm_ComputerSystem target = e.TargetInstance;
            VirtualMachines.Add(new VirtualMachine(target));
            UpdateSummaryInformation();
            //OnVmCreated(this, target);
        }

        private void OnDeleteInstance(object sender, WmiEvent<IMsvm_ComputerSystem> e)
        {
            IMsvm_ComputerSystem target = e.TargetInstance;

            VirtualMachine removed = null;
            {
                lock(vmListLock)
                for (int i = 0; i < VirtualMachines.Count; ++i)
                {
                    if (VirtualMachines[i].Guid == target.Name)
                    {
                        removed = VirtualMachines[i];
                        VirtualMachines.RemoveAt(i);
                        break;
                    }
                }
            }
            if (removed != null)
            {
                try
                {
                    OnVmDeleted(this, removed);
                }
                catch(Exception)
                {
                }
            }
        }

        private void OnModifyInstance(object sender, WmiEvent<IMsvm_ComputerSystem> e)
        {
            IMsvm_ComputerSystem target = e.TargetInstance;
            IMsvm_ComputerSystem previous = e.PreviousInstance;
            //OnVmModified(this, previous, target);

            lock (vmListLock)
            {
                foreach (VirtualMachine vm in VirtualMachines)
                {
                    if (vm.Guid == target.Name)
                    {
                        vm.UpdateMainInformation(target);
                        break;
                    }
                }
            }
        }

        private IMsvm_VirtualSystemSettingData[] CreateSettingsArray()
        {
            return (from vm in scope.GetInstances<IMsvm_ComputerSystem>() where vm.Caption == "Virtual Machine"
                    from settings in vm.GetAssociated<IMsvm_VirtualSystemSettingData>("Msvm_SettingsDefineState")
                    select settings).ToArray();
        }

        private IEnumerable<VirtualMachine> GetVMs()
        {
            return from vm in scope.GetInstances<IMsvm_ComputerSystem>() where vm.Caption == "Virtual Machine" select new VirtualMachine(vm);
        }

        private void UpdateSummaryInformation()
        {
            lock (vmListLock)
            {
                uint[] infoRequest = new uint[] {
                    0, // Name (Guid)
                    4, // NumberOfProcessors
                    7, // ThumbnailImage (Large 320x240 RGB565 format)
                    10, // Version
                    101, // ProcessorLoad
                    103, // MemoryUsage
                    104, // Heartbeat
                    105, // Uptime
                    112 // MemoryAvailable
                };
                uint err = vsms.GetSummaryInformation(infoRequest, CreateSettingsArray(), out IMsvm_SummaryInformation[]? summary);
                if (err != 0)
                {
                    return;
                }

                foreach (IMsvm_SummaryInformation info in summary)
                {
                    foreach (VirtualMachine vm in VirtualMachines)
                    {
                        if (vm.Guid == info.Name)
                        {
                            vm.UpdateSummaryInformation(info);
                            break;
                        }
                    }
                }
            }
        }

        // events
        //public event VmCreateHandler OnVmCreated;
        public event VmDeleteHandler OnVmDeleted;
        //public event VmModifyHandler OnVmModified;

        // data for bindings
        public ObservableCollection<VirtualMachine> VirtualMachines;

        // singleton vars
        public delegate void VmCreateHandler(object sender, IMsvm_ComputerSystem target);
        public delegate void VmDeleteHandler(object sender, VirtualMachine target);
        public delegate void VmModifyHandler(object sender, IMsvm_ComputerSystem previous, IMsvm_ComputerSystem target);
        private IMsvm_VirtualSystemManagementService vsms;
        private WmiSubscription<IMsvm_ComputerSystem> creationWatcher;
        private WmiSubscription<IMsvm_ComputerSystem> modificationWatcher;
        private WmiSubscription<IMsvm_ComputerSystem> deletionWatcher;
        private object vmListLock = new object();
    }
}
