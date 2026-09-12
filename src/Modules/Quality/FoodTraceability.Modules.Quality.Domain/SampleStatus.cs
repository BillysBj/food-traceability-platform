namespace FoodTraceability.Modules.Quality.Domain;

// D-47: blocking and releasing belong to the lot, not to the sample.
public enum SampleStatus
{
    Pending,
    Pass,
    Fail,
}
