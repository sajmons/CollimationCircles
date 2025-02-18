using System.Collections.Generic;

namespace CollimationCircles.Services
{
    public interface ICommandBuilder
    {
        public List<string> GetParameterList();
    }
}
