namespace Qlcheck.Checks;

public interface ICheck
{
    string Id { get; }

    string Language { get; }

    bool EnabledByDefault => true;

    // False for a Check that is known by id but not built yet; selecting it is an error.
    bool IsImplemented => true;
}
