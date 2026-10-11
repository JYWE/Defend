namespace Defend.Simulation
{
    public readonly struct CommandResult
    {
        public long Sequence { get; }
        public bool Succeeded { get; }
        public string Message { get; }
        public long BuildingId { get; }

        internal CommandResult(long sequence, bool succeeded, string message, long buildingId)
        {
            Sequence = sequence;
            Succeeded = succeeded;
            Message = message;
            BuildingId = buildingId;
        }
    }
}
