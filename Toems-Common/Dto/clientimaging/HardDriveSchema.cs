﻿using System.Collections.Generic;

namespace Toems_Common.Dto.clientimaging
{
    public class HardDriveSchema
    {
        public string BootPartition { get; set; }
        public string Guid { get; set; }
        public string IsValid { get; set; }
        public string Message { get; set; }
        public string PartitionType { get; set; }
        public int PhysicalPartitionCount { get; set; }
        public List<PhysicalPartition> PhysicalPartitions { get; set; }
        public int SchemaHdNumber { get; set; }
        public string UsesLvm { get; set; }
    }
}