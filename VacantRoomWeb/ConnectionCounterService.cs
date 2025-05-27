namespace VacantRoomWeb
{
    public class ConnectionCounterService
    {
        private int _count = 0;

        public int Increment()
        {
            return Interlocked.Increment(ref _count);
        }

        public int Decrement()
        {
            return Interlocked.Decrement(ref _count);
        }

        public int GetCount()
        {
            return _count;
        }
    }

}
