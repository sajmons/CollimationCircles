namespace CollimationCircles.Models
{
    public interface ICountable
    {
        public bool IsCountable { get; set; }
        public int MaxCount { get; set; }
    }
}
