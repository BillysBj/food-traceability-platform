namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed class ReleaseLotBlockNotFoundException : Exception
{
    public ReleaseLotBlockNotFoundException() : base("The block does not exist for this lot in this organization.")
    {
    }
}

public sealed class LotBlockAlreadyReleasedException : Exception
{
    public LotBlockAlreadyReleasedException() : base("The lot block has already been released.")
    {
    }
}
