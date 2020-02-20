﻿using CloneDeploy_Services;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toems_Common.Dto.clientpartition;
using Toems_Common.Dto.imageschemabe;
using Toems_Common.Entity;

namespace Toems_Service
{
    public class CustomComparer : IComparer<long>
    {
        public int Compare(long x, long y)
        {
            return x.CompareTo(y);
        }
    }

    public class ServiceClientPartitionSchema
    {
        private readonly int _hdToGet;
        private readonly ImageProfileWithImage _imageProfile;

        private readonly DtoImageSchema _imageSchema;
        private readonly long _newHdSize;
        private readonly ILog log = LogManager.GetLogger(typeof(ServiceClientPartitionSchema));

        public ServiceClientPartitionSchema(int hdToGet, string newHdSize, ImageProfileWithImage imageProfile,
            string partitionPrefix)
        {
            _hdToGet = hdToGet;
            _newHdSize = (long)(Convert.ToInt64(newHdSize) * .99);
            _imageProfile = imageProfile;
            PrimaryAndExtendedPartitions = new List<ClientPartition>();
            LogicalPartitions = new List<ClientPartition>();
            LogicalVolumes = new List<ClientLogicalVolume>();
            VolumeGroupHelpers = new List<ClientVolumeGroupHelper>();
            _imageSchema = new ServiceClientPartition(_imageProfile).GetImageSchema();
        }

        private string BootPart { get; set; }
        public string DebugStatus { get; set; }
        public ExtendedPartitionHelper ExtendedPartitionHelper { get; set; }

        public long FirstPartitionStartSector { get; set; }
        private int HdNumberToGet { get; set; }

        private int LbsByte { get; set; }
        public List<ClientPartition> LogicalPartitions { get; set; }
        public List<ClientLogicalVolume> LogicalVolumes { get; set; }
        private long NewHdBlk { get; set; }
        public List<ClientPartition> PrimaryAndExtendedPartitions { get; set; }
        public List<ClientVolumeGroupHelper> VolumeGroupHelpers { get; set; }

        /// <summary>
        ///     Generates the partitioning layout used for the client when restoring an image.
        /// </summary>
        public ServiceClientPartitionSchema GenerateClientSchema()
        {
            var activeCounter = _hdToGet;
            HdNumberToGet = _hdToGet;
            //Look for first active hd
            if (!_imageSchema.HardDrives[HdNumberToGet].Active)
            {
                while (activeCounter <= _imageSchema.HardDrives.Count())
                {
                    if (_imageSchema.HardDrives[activeCounter - 1].Active)
                    {
                        HdNumberToGet = activeCounter - 1;
                    }
                    activeCounter++;
                }
            }

            LbsByte = Convert.ToInt32(_imageSchema.HardDrives[HdNumberToGet].Lbs); //logical block size in bytes
            NewHdBlk = Convert.ToInt64(_newHdSize) / LbsByte; //size of client hard drive in block

            //Find the Boot partition
            if (_imageSchema.HardDrives[HdNumberToGet].Boot.Length > 0)
                BootPart =
                    _imageSchema.HardDrives[HdNumberToGet].Boot.Substring(
                        _imageSchema.HardDrives[HdNumberToGet].Boot.Length - 1, 1);

            if (!PrimaryAndExtendedPartitionLayout())
                return null;

            if (ExtendedPartitionHelper.HasLogical)
                if (!LogicalPartitionLayout())
                    return null;

            if (VolumeGroupHelpers.Any())
                if (!LogicalVolumeLayout())
                    return null;

            //Order partitions based of block start
            PrimaryAndExtendedPartitions =
                PrimaryAndExtendedPartitions.OrderBy(part => part.Start, new CustomComparer()).ToList();
            LogicalPartitions = LogicalPartitions.OrderBy(part => part.Start, new CustomComparer()).ToList();

            return this;
        }

        private bool LogicalPartitionLayout()
        {
            // Try to resize logical to fit inside newly created extended
            var percentCounter = -.1;
            var upSizeLock = new Dictionary<string, long>();
            var logicalPartLayoutVerified = false;
            while (!logicalPartLayoutVerified)
            {
                percentCounter += .1;
                var isError = false;
                LogicalPartitions.Clear();
                double totalExtendedPercentage = 0;

                var partCounter = -1;
                foreach (var part in _imageSchema.HardDrives[HdNumberToGet].Partitions)
                {
                    partCounter++;
                    if (part.Type.ToLower() != "logical")
                        continue;

                    var clientPartition = new ClientPartition
                    {
                        IsBoot = BootPart == part.Number,
                        Number = part.Number,
                        Start = part.Start,
                        Type = part.Type,
                        FsId = part.FsId,
                        Uuid = part.Uuid,
                        Guid = part.Guid,
                        FsType = part.FsType
                    };

                    var logicalPartitionHelper = new ServiceClientPartition(_imageProfile).Partition(HdNumberToGet,
                        partCounter, _newHdSize);

                    var percentOfExtendedForThisPartition = (double)logicalPartitionHelper.MinSizeBlk /
                                                            ExtendedPartitionHelper.AgreedSizeBlk;
                    var tmpClientPartitionSizeBlk = logicalPartitionHelper.MinSizeBlk;

                    if (upSizeLock.ContainsKey(part.Number))
                        tmpClientPartitionSizeBlk = upSizeLock[part.Number];
                    else
                    {
                        if (logicalPartitionHelper.IsDynamicSize)
                        {
                            clientPartition.SizeIsDynamic = true;
                            var percentOfOrigDrive = Convert.ToInt64(part.Size) /
                                                     (double)
                                                         Convert.ToInt64(_imageSchema.HardDrives[HdNumberToGet].Size);

                            if (Convert.ToInt64(NewHdBlk * percentOfOrigDrive) < logicalPartitionHelper.MinSizeBlk)
                            {
                                //This will never work because each loop only gets smaller
                                tmpClientPartitionSizeBlk =
                                    Convert.ToInt64(NewHdBlk * (percentOfOrigDrive + percentCounter / 100));

                                if (logicalPartitionHelper.MinSizeBlk < tmpClientPartitionSizeBlk)
                                    upSizeLock.Add(part.Number, tmpClientPartitionSizeBlk);
                            }
                            else
                            {
                                tmpClientPartitionSizeBlk = percentOfOrigDrive - percentCounter / 100 <= 0
                                    ? Convert.ToInt64(NewHdBlk * percentOfOrigDrive)
                                    : Convert.ToInt64(NewHdBlk * (percentOfOrigDrive - percentCounter / 100));

                                percentOfExtendedForThisPartition = (double)tmpClientPartitionSizeBlk /
                                                                    ExtendedPartitionHelper.AgreedSizeBlk;
                            }
                        }
                    }

                    if (logicalPartitionHelper.MinSizeBlk > tmpClientPartitionSizeBlk)
                    {
                        isError = true;
                        break;
                    }

                    totalExtendedPercentage += percentOfExtendedForThisPartition;
                    clientPartition.Size = tmpClientPartitionSizeBlk;

                    LogicalPartitions.Add(clientPartition);

                    if (logicalPartitionHelper.PartitionHasVolumeGroup)
                    {
                        logicalPartitionHelper.VolumeGroupHelper.AgreedPvSizeBlk = tmpClientPartitionSizeBlk;
                        VolumeGroupHelpers.Add(logicalPartitionHelper.VolumeGroupHelper);
                    }
                }

                //Could not determine a partition layout that works with this hard drive
                if (isError && percentCounter > 99)
                {
                    log.Error(JsonConvert.SerializeObject(PrimaryAndExtendedPartitions));
                    log.Error(JsonConvert.SerializeObject(LogicalPartitions));
                    return false;
                }

                //This partition size doesn't work, continuation of break from earlier
                if (isError)
                {
                    continue;
                }

                if (totalExtendedPercentage <= 1)
                {
                    long totalAllocatedBlk = 0;
                    var dynamicPartitionCount = 0;
                    //If totalPercentage is too far below 1 try to increase size of available resizable partitions
                    if (totalExtendedPercentage < .98)
                    {
                        foreach (var partition in LogicalPartitions)
                        {
                            totalAllocatedBlk += Convert.ToInt64(partition.Size);
                            if (partition.SizeIsDynamic)
                                dynamicPartitionCount++;
                        }
                        var totalUnallocated = ExtendedPartitionHelper.AgreedSizeBlk - totalAllocatedBlk;
                        if (dynamicPartitionCount > 0)
                        {
                            foreach (
                                var partition in LogicalPartitions.Where(partition => partition.SizeIsDynamic))
                            {
                                partition.Size =
                                    partition.Size + totalUnallocated / dynamicPartitionCount;
                            }
                        }
                    }
                    logicalPartLayoutVerified = true;
                }

                //Theoretically should never hit this, but added to prevent infinite loop
                if (percentCounter > 100)
                {
                    return false;
                }
            }

            return true;
        }

        private bool LogicalVolumeLayout()
        {
            var upSizeLock = new Dictionary<string, long>();
            //Try to resize lv to fit inside newly created lvm
            foreach (var volumeGroup in VolumeGroupHelpers)
            {
                //Tell the volume group it has a size of the physical volume to work with * 99% to account for errors to allow alittle over
                //volumeGroup.AgreedPvSizeBlk = Convert.ToInt64(volumeGroup.AgreedPvSizeBlk * .99);
                foreach (var partition in _imageSchema.HardDrives[HdNumberToGet].Partitions)
                {
                    //Find the partition this volume group belongs to
                    if (_imageSchema.HardDrives[HdNumberToGet].Name + partition.Prefix + partition.Number !=
                        volumeGroup.Pv) continue;
                    var singleLvVerified = false;

                    var percentCounter = -.1;

                    while (!singleLvVerified)
                    {
                        percentCounter += .1;
                        double totalPvPercentage = 0;
                        LogicalVolumes.Clear();
                        if (!partition.Active)
                            continue;

                        var isError = false;
                        foreach (var lv in partition.VolumeGroup.LogicalVolumes)
                        {
                            if (!lv.Active)
                                continue;

                            var clientPartitionLv = new ClientLogicalVolume
                            {
                                Name = lv.Name,
                                Vg = lv.VolumeGroup,
                                Uuid = lv.Uuid,
                                VgUuid = volumeGroup.Uuid,
                                FsType = lv.FsType
                            };

                            var logicalVolumeHelper = new ServiceClientPartition(_imageProfile).LogicalVolume(lv, LbsByte,
                                _newHdSize, HdNumberToGet);
                            var percentOfPvForThisLv = (double)logicalVolumeHelper.MinSizeBlk /
                                                       volumeGroup.AgreedPvSizeBlk;
                            var tmpClientPartitionSizeLvBlk = logicalVolumeHelper.MinSizeBlk;

                            if (volumeGroup.IsFusion)
                            {
                                clientPartitionLv.Size = 0;
                                LogicalVolumes.Add(clientPartitionLv);
                                singleLvVerified = true;
                                continue;
                            }

                            if (upSizeLock.ContainsKey(lv.Name))
                                tmpClientPartitionSizeLvBlk = upSizeLock[lv.Name];
                            else
                            {
                                if (logicalVolumeHelper.IsDynamicSize)
                                {
                                    clientPartitionLv.SizeIsDynamic = true;
                                    var percentOfOrigDrive = Convert.ToInt64(lv.Size) /
                                                             (double)
                                                                 Convert.ToInt64(
                                                                     _imageSchema.HardDrives[HdNumberToGet].Size);

                                    if (Convert.ToInt64(NewHdBlk * percentOfOrigDrive) < logicalVolumeHelper.MinSizeBlk)
                                    {
                                        //This will never work because each loop only gets smaller
                                        tmpClientPartitionSizeLvBlk =
                                            Convert.ToInt64(NewHdBlk * (percentOfOrigDrive + percentCounter / 100));

                                        if (logicalVolumeHelper.MinSizeBlk < tmpClientPartitionSizeLvBlk)
                                            upSizeLock.Add(lv.Name, tmpClientPartitionSizeLvBlk);
                                    }
                                    else
                                    {
                                        if (percentOfOrigDrive - percentCounter / 100 <= 0)
                                            tmpClientPartitionSizeLvBlk =
                                                Convert.ToInt64(NewHdBlk * percentOfOrigDrive);
                                        else
                                            tmpClientPartitionSizeLvBlk =
                                                Convert.ToInt64(NewHdBlk *
                                                                (percentOfOrigDrive - percentCounter / 100));
                                    }
                                    percentOfPvForThisLv = (double)tmpClientPartitionSizeLvBlk /
                                                           volumeGroup.AgreedPvSizeBlk;
                                }
                            }

                            if (logicalVolumeHelper.MinSizeBlk > tmpClientPartitionSizeLvBlk)
                            {
                                isError = true;
                                break;
                            }

                            clientPartitionLv.Size = tmpClientPartitionSizeLvBlk;
                            totalPvPercentage += percentOfPvForThisLv;
                            LogicalVolumes.Add(clientPartitionLv);
                        }

                        //Could not determine a partition layout that works with this hard drive
                        if (isError && percentCounter > 99)
                            return false;

                        //This partition size doesn't work, continuation of break from earlier
                        if (isError)
                            continue;

                        if (totalPvPercentage <= 1)
                        {
                            long totalAllocatedBlk = 0;
                            var dynamicPartitionCount = 0;
                            //If totalPercentage is too far below 1 try to increase size of available resizable partitions
                            if (totalPvPercentage < .95)
                            {
                                foreach (var lv in LogicalVolumes)
                                {
                                    totalAllocatedBlk += Convert.ToInt64(lv.Size);
                                    if (lv.SizeIsDynamic)
                                        dynamicPartitionCount++;
                                }
                                var totalUnallocated = volumeGroup.AgreedPvSizeBlk - totalAllocatedBlk;
                                if (dynamicPartitionCount > 0)
                                {
                                    foreach (var lv in LogicalVolumes.Where(lv => lv.SizeIsDynamic))
                                    {
                                        lv.Size = lv.Size + totalUnallocated / dynamicPartitionCount;
                                    }
                                }
                            }
                            singleLvVerified = true;
                        }

                        //Theoretically should never hit this, but added to prevent infinite loop
                        if (percentCounter > 100)
                            return false;
                    }
                }
            }
            return true;
        }

        private bool PrimaryAndExtendedPartitionLayout()
        {
            //Try to determine a layout for each primary or extended partition that will be able to fit logical partitions
            //or logical volumes in.  Also if the partition is logical and is the physical volume for a volume group determine 
            // a size that will work for all logical volumes
            ExtendedPartitionHelper = new ServiceClientPartition(_imageProfile).ExtendedPartition(HdNumberToGet,
                _newHdSize);
            var upSizeLock = new Dictionary<string, long>();

            var percentCounter = -.1;
            var partitionLayoutVerified = false;
            while (!partitionLayoutVerified)
            {
                //percentCounter++;

                percentCounter += .1;
                var isError = false;
                double totalHdPercentage = 0;

                PrimaryAndExtendedPartitions.Clear();
                VolumeGroupHelpers.Clear();
                FirstPartitionStartSector = Convert.ToInt64(_imageSchema.HardDrives[HdNumberToGet].Partitions[0].Start);
                var partCounter = -1;

                foreach (var schemaPartition in _imageSchema.HardDrives[HdNumberToGet].Partitions)
                {
                    partCounter++;

                    //Determine what sector the first partition should start at
                    if (Convert.ToInt64(schemaPartition.Start) < FirstPartitionStartSector)
                        FirstPartitionStartSector = Convert.ToInt64(schemaPartition.Start);

                    if (!schemaPartition.Active)
                        continue;
                    if (schemaPartition.Type.ToLower() == "logical")
                        continue;

                    var clientPartition = new ClientPartition
                    {
                        IsBoot = BootPart == schemaPartition.Number,
                        Number = schemaPartition.Number,
                        Start = schemaPartition.Start,
                        Type = schemaPartition.Type,
                        FsId = schemaPartition.FsId,
                        Uuid = schemaPartition.Uuid,
                        Guid = schemaPartition.Guid,
                        FsType = schemaPartition.FsType
                    };

                    var partitionHelper = new ServiceClientPartition(_imageProfile).Partition(HdNumberToGet, partCounter,
                        _newHdSize);

                    var percentOfHdForThisPartition = (double)partitionHelper.MinSizeBlk / NewHdBlk;
                    var tmpClientPartitionSizeBlk = partitionHelper.MinSizeBlk;

                    if (partitionHelper.IsDynamicSize)
                    {
                        clientPartition.SizeIsDynamic = true;
                        var percentOfOrigDrive = Convert.ToInt64(schemaPartition.Size) /
                                                 (double)Convert.ToInt64(_imageSchema.HardDrives[HdNumberToGet].Size);

                        //Change the resized partition size based off original percentage and percentCounter loop
                        //This is the active part of the loop that lowers the partition size based on each iteration

                        //If a partition's used space is almost maxed out to the size of the partition this can cause
                        //problems with calculations. 

                        if (upSizeLock.ContainsKey(schemaPartition.Number))
                            tmpClientPartitionSizeBlk = upSizeLock[schemaPartition.Number];

                        else
                        {
                            if (Convert.ToInt64(NewHdBlk * percentOfOrigDrive) < partitionHelper.MinSizeBlk)
                            {
                                //This will never work because each loop only gets smaller

                                tmpClientPartitionSizeBlk =
                                    Convert.ToInt64(NewHdBlk * (percentOfOrigDrive + percentCounter / 100));
                                if (partitionHelper.MinSizeBlk < tmpClientPartitionSizeBlk)
                                    upSizeLock.Add(schemaPartition.Number, tmpClientPartitionSizeBlk);
                            }
                            else
                            {
                                tmpClientPartitionSizeBlk = percentOfOrigDrive - percentCounter / 100 <= 0
                                    ? Convert.ToInt64(NewHdBlk * percentOfOrigDrive)
                                    : Convert.ToInt64(NewHdBlk * (percentOfOrigDrive - percentCounter / 100));
                            }
                        }
                        //Add the percent of this partition used to the total percent used to make sure we don't go over
                        //100% of the size of the new drive.

                        //Each logical partition requires and extra 1 mb added to the size of the extended partition.
                        if (clientPartition.Type.ToLower() == "extended")
                            percentOfHdForThisPartition = ((double)tmpClientPartitionSizeBlk +
                                                           (1048576 / LbsByte * ExtendedPartitionHelper.LogicalCount +
                                                            1048576 / LbsByte)) /
                                                          NewHdBlk;
                        else
                            percentOfHdForThisPartition = (double)tmpClientPartitionSizeBlk / NewHdBlk;
                    }

                    if (partitionHelper.MinSizeBlk > tmpClientPartitionSizeBlk)
                    {
                        isError = true;
                        break;
                    }

                    if (clientPartition.Type.ToLower() == "extended")
                        ExtendedPartitionHelper.AgreedSizeBlk = tmpClientPartitionSizeBlk;

                    if (partitionHelper.PartitionHasVolumeGroup)
                    {
                        partitionHelper.VolumeGroupHelper.AgreedPvSizeBlk = tmpClientPartitionSizeBlk;
                        VolumeGroupHelpers.Add(partitionHelper.VolumeGroupHelper);
                    }

                    clientPartition.Size = tmpClientPartitionSizeBlk;
                    totalHdPercentage += percentOfHdForThisPartition;
                    PrimaryAndExtendedPartitions.Add(clientPartition);
                }

                //Could not determine a partition layout that works with this hard drive
                if (isError && percentCounter > 99)
                {
                    return false;
                }

                //This partition size doesn't work, continuation of break from earlier
                if (isError)
                    continue;

                DebugStatus += percentCounter + "\r\n";
                if (totalHdPercentage <= 1)
                {
                    //if totalPercentage is < 1 then layout has been verified to work
                    partitionLayoutVerified = true;

                    //If totalPercentage is too far below 1 try to increase size of available resizable partitions
                    long totalAllocatedBlk = 0;
                    var dynamicPartitionCount = 0;
                    if (totalHdPercentage < .98)
                    {
                        foreach (var partition in PrimaryAndExtendedPartitions)
                        {
                            totalAllocatedBlk += Convert.ToInt64(partition.Size);
                            if (partition.SizeIsDynamic)
                                dynamicPartitionCount++;
                        }
                        var totalUnallocated = NewHdBlk - totalAllocatedBlk;
                        if (dynamicPartitionCount > 0)
                        {
                            foreach (
                                var partition in
                                    PrimaryAndExtendedPartitions.Where(partition => partition.SizeIsDynamic))
                            {
                                partition.Size = partition.Size + totalUnallocated / dynamicPartitionCount;
                                if (partition.Type.ToLower() == "extended")
                                    ExtendedPartitionHelper.AgreedSizeBlk = Convert.ToInt64(partition.Size);
                                for (var i = 0; i < VolumeGroupHelpers.Count(); i++)
                                    if (_imageSchema.HardDrives[HdNumberToGet].Name + partition.Number ==
                                        VolumeGroupHelpers[i].Pv)
                                        VolumeGroupHelpers[i].AgreedPvSizeBlk = Convert.ToInt64(partition.Size);
                            }
                        }
                    }
                }

                //Theoretically should never hit this, but added to prevent infinite loop
                if (percentCounter > 100)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
