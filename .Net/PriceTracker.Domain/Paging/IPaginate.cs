namespace PriceTracker.Domain.Paging
{
    public interface IPaginate<T>
    {
        int From { get; }

        int Offset { get; }

        int Size { get; }

        int Count { get; }

        int Pages { get; }

        IList<T> Items { get; }

        bool HasPrevious { get; }

        bool HasNext { get; }
    }
}