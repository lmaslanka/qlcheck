namespace Qlcheck.Checks;

public interface ICheck
{
    string Id { get; }

    string Language { get; }

    bool EnabledByDefault => true;
}
