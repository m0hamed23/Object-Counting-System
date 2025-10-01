using System.Collections.Generic;

namespace CountingWebAPI.Models.DTOs
{
    public class RoiDataDto
    {
        public int CameraIndex { get; set; }
        public List<List<double>> Roi { get; set; } = new List<List<double>>();
    }
}

//