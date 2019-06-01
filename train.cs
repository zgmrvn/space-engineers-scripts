private Vector3D unload = new Vector3(-51682.91, -27953.14, 16193.54);
private Vector3D load = new Vector3(-51508.45, -28377.40, 16021.13);
private const float startToSlowDown = 170f;
private const float threshold = 10f;
private const float maxSpeed = 100f;
private const float dockingSpeed = 5f;
private const float speedMarginOfError = maxSpeed * 0.05f;
private const float operationalCharge = 0.99f;

private bool run = false;
private List<IMyTerminalBlock> wheels;
private IMyRadioAntenna antenna;
private IMyReflectorLight frontLight;
private IMyReflectorLight backLight;
private IMyShipController remoteControl;
private IMyShipConnector frontConnector;
private IMyShipConnector backConnector;
private IMyShipConnector frontEjector;
private IMyShipConnector backEjector;
private IMyBatteryBlock battery;
private float maxStoredPower = 0;
private IMyInventory frontContainer;
private IMyInventory backContainer;
private double velocity = 0;
private string target;
private Vector3D targetPosition;

// temp
private int tempCounter = 0;
private double targetVelocity = 0;
private DateTime timer;

private enum Phase
{
    Crusing,
    Deceleration,
    Docking,
    Docked
}

private Phase phase = Phase.Crusing;

#region Contructor
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    run = true;

    // Wheels.
    wheels = new List<IMyTerminalBlock>();
    (GridTerminalSystem.GetBlockGroupWithName("Wheels") as IMyBlockGroup).GetBlocks(wheels);

    // Antenna.
    antenna = GridTerminalSystem.GetBlockWithName("Antenna") as IMyRadioAntenna;

    // Lights.
    frontLight = GridTerminalSystem.GetBlockWithName("Light Front") as IMyReflectorLight;
    backLight = GridTerminalSystem.GetBlockWithName("Light Back") as IMyReflectorLight;

    // Remote control.
    remoteControl = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyShipController;

    // Ejector.
    frontEjector = GridTerminalSystem.GetBlockWithName("Ejector Front") as IMyShipConnector;
    backEjector = GridTerminalSystem.GetBlockWithName("Ejector Back") as IMyShipConnector;

    // Connectors.
    frontConnector = GridTerminalSystem.GetBlockWithName("Connector Front") as IMyShipConnector;
    backConnector = GridTerminalSystem.GetBlockWithName("Connector Back") as IMyShipConnector;

    // Battery.
    battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock;
    maxStoredPower = battery.MaxStoredPower;

    // Containers.
    frontContainer = (GridTerminalSystem.GetBlockWithName("Container Front") as IMyCargoContainer).GetInventory();
    backContainer = (GridTerminalSystem.GetBlockWithName("Container Back") as IMyCargoContainer).GetInventory();

    // Set initial target.
    target = "Load";
    targetPosition = load;

    // ChangeDestination();

    targetVelocity = maxSpeed;
    //InvertPropulsion();
    ToggleWheels(true);

    //frontLight.ApplyAction("OnOff_On");
}
#endregion

public void Main(string argument, UpdateType updateSource)
{
    velocity = remoteControl.GetShipVelocities().LinearVelocity.Length() * 3600 / 1000;


    Echo("Target: " + target + " (" + DistanceToTarget().ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "m)");
    Echo("Phase: " + phase.ToString());
    Echo("Velocity: " + velocity.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
    Echo("MOE:" + speedMarginOfError.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
    Echo("TV:" + targetVelocity.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));

    if (tempCounter > 1)
    {
        run = false;
    }

    if (!run)
    {
        ToggleWheels(false);
        return;
    }

    #region Speed regulation
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
    #endregion

    #region Phases
    // Crusing phase.
    if (DistanceToTarget() < startToSlowDown && phase == Phase.Crusing)
    {
        phase = Phase.Deceleration;
        targetVelocity = maxSpeed;
    }

     // Deceleration phase.
    else if (phase == Phase.Deceleration)
    {
        Echo("Deceleration: " + DecelerationStatus().ToString());
        Echo("Target speed: " + (DecelerationStatus() * maxSpeed).ToString());

        // If the vehicle slows down below the docking speed limit,
        // we move to the next phase.
        if (velocity < dockingSpeed)
        {
            targetVelocity = dockingSpeed;
            ConnectorsEnabled();
            phase = Phase.Docking;
            
            return;
        }

        // Otherwise, we set the target velocity based on the deceleration progression.
        targetVelocity = DecelerationStatus() * maxSpeed;
    }

    // Docking phase.
    else if (phase == Phase.Docking)
    {
        // look for a connector
        // when found, stop and connect
        // then move to docked phase
        if (IsConnectable())
        {
            // ToggleBrakes(true);
            Connect();
            LightsDisabled();

            phase = Phase.Docked;
        }
    }

    // Docked phase.
    else if (phase == Phase.Docked)
    {

        // if battery is full && cargo is full
        // or timer > than...
        // IsCharged()
        // IsFull()
        if (IsCharged())
        {
            Disconnect();
            ConnectorsDisabled();
            
            targetVelocity = maxSpeed;

            ChangeDestination();
            InvertPropulsion();
            InvertLights();
            LightsEnabled();
            ToggleWheels(true);

            phase = Phase.Crusing;

            tempCounter++;
        }
    }
    #endregion
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

private void InvertPropulsion()
{
    foreach (IMyMotorSuspension wheel in wheels)
    {
        wheel.InvertPropulsion = !wheel.InvertPropulsion;
    } 
}

private double DistanceToTarget()
{
    return Vector3D.Distance(antenna.GetPosition(), targetPosition);
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

private void ChangeDestination()
{
    target = target == "Load" ? "Unload" : "Load";
    targetPosition = target == "Load" ? load : unload;
}

private double DecelerationStatus()
{
    return (DistanceToTarget() - (startToSlowDown - threshold)) / (startToSlowDown - threshold);
}

private bool IsCharged()
{
    return battery.CurrentStoredPower / maxStoredPower > operationalCharge;
}

private bool IsFull()
{
    return frontContainer.IsFull && backContainer.IsFull;
}

// Lights.
private void LightsEnabled()
{
    frontLight.Enabled = true;
    backLight.Enabled = true;
}

private void LightsDisabled()
{
    frontLight.Enabled = false;
    backLight.Enabled = false;
}

private void InvertLights()
{
    Color color = backLight.Color;

    backLight.Color = frontLight.Color;
    frontLight.color = color;
}

// Connectors.
private void ConnectorsEnabled()
{
    frontConnector.Enabled = true;
    backConnector.Enabled = true;
}

private void ConnectorsDisabled()
{
    frontConnector.Enabled = false;
    backConnector.Enabled = false;
}

private void Connect()
{
    frontConnector.Connect();
    backConnector.Connect();
}

private void Disconnect()
{
    frontConnector.Disconnect();
    backConnector.Disconnect();
}

private bool IsConnectable()
{
    return frontConnector.Status == MyShipConnectorStatus.Connectable || backConnector.Status == MyShipConnectorStatus.Connectable;
}


private bool IsConnected()
{
    return frontConnector.Status == MyShipConnectorStatus.Connected || backConnector.Status == MyShipConnectorStatus.Connected;
}

// Ejectors.
// private void 