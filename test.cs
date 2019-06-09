private List<IMyTerminalBlock> wheels;
private IMyShipController remoteControl;
private double velocity = 0;
private double targetVelocity = 2;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    // Wheels.
    wheels = new List<IMyTerminalBlock>();
    (GridTerminalSystem.GetBlockGroupWithName("Wheels") as IMyBlockGroup).GetBlocks(wheels);

    // Remote control.
    remoteControl = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyShipController;

    // GO !
    InvertPropulsion();
    ToggleWheels(true);
}

public void Main(string argument, UpdateType updateSource)
{
    velocity = remoteControl.GetShipVelocities().LinearVelocity.Length() * 3600 / 1000;


    if (velocity < targetVelocity - 1)
    {
        ToggleWheels(true);
    }

    else if (velocity > targetVelocity + 1)
    {
        ToggleBrakes(true);
    }

    else if (velocity > targetVelocity)
    {
        ToggleBrakes(false);
        ToggleWheels(false);
    }
}

private void ToggleWheels(bool state)
{
    if (state)
    {
        ToggleBrakes(false);
    }

    string action = "OnOff_" + (state ? "On" : "Off");
    
    foreach (IMyMotorSuspension wheel in wheels)
    {
        wheel.ApplyAction(action);
    }
}

private void ToggleBrakes(bool state)
{
    if (state)
    {
        ToggleWheels(false);
    }

    foreach (IMyMotorSuspension wheel in wheels)
    {
        wheel.Brake = state;
    } 
}

private void InvertPropulsion()
{
    foreach (IMyMotorSuspension wheel in wheels)
    {
        wheel.InvertPropulsion = !wheel.InvertPropulsion;
    } 
}