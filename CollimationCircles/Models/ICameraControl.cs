namespace CollimationCircles.Models
{
    public interface ICameraControl
    {
        public ControlType Name { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Step { get; set; }
        public int Default { get; set; }
        public int Value { get; set; }
        public string Flags { get; set; }
        public void SetDefault();
    }
}
