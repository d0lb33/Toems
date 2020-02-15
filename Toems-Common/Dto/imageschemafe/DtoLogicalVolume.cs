﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toems_Common.Dto.imageschemafe
{
    public class DtoLogicalVolume
    {
        public bool Active { get; set; }
        public string CustomSize { get; set; }
        public string CustomSizeUnit { get; set; }
        public bool ForceFixedSize { get; set; }
        public string FsType { get; set; }
        public string Name { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public string UsedMb { get; set; }
        public string Uuid { get; set; }
        public string VolumeGroup { get; set; }
        public string VolumeSize { get; set; }
    }
}
