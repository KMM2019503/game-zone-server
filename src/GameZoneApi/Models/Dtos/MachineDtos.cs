namespace GameZoneApi.Models;

public record MachineResponse(
    Guid Id,
    string Name,
    string Specs,
    decimal HourlyRate,
    bool IsActive)
{
    public static MachineResponse From(Machine machine) =>
        new(machine.Id, machine.Name, machine.Specs, machine.HourlyRate, machine.IsActive);
}
