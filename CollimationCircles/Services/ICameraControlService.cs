using System;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(string propertyname, double value, StreamSource streamSource);
        public void Open();
        public void Release();
    }
}
