namespace Qlcheck;

public interface ICheck
{
    string Id { get; }

    bool EnabledByDefault => true;
}
