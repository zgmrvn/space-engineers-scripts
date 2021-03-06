private enum Phase
{
    Crusing,
    Deceleration,
    Docking,
    Docked
}

private enum Target
{
    Load,
    Unload
}

private Vector3D unload = new Vector3(-51681.95, -27954.90, 16192.60);
private Vector3D load = new Vector3(-51129.13, -29289.22, 15648.61);
private const float startToSlowDown = 250f;
private const float connectorSafeDistance = 10f;
private const float maxSpeed = 100f;
private const float maxVelocity = maxSpeed * 1000 / 3600;
private const float dockingVelocity = 2f;
private const float operationalCharge = 0.98f;

private List<IMyTerminalBlock> wheels;
private IMyRadioAntenna antenna;
private IMyReflectorLight frontLight;
private IMyReflectorLight backLight;
private IMyShipController remoteControl;
private List<IMyShipConnector> connectors;
private List<IMyShipConnector> ejectors;

// Battery.
private IMyBatteryBlock battery;
private float maxStoredPower = 0;

// Containers.
private List<IMyInventory> containers;
private float containersLastVolume;
private DateTime loadingTimeout;
private const int LoadingTimeout = 20;

private double velocity = 0;
private Target target;
private Vector3D targetPosition;
private double targetVelocity = 0;
private Phase phase = Phase.Crusing;

#region Contructor
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

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

    // Ejectors.
    ejectors = new List<IMyShipConnector>();
    ejectors.Add(GridTerminalSystem.GetBlockWithName("Ejector Front") as IMyShipConnector);
    ejectors.Add(GridTerminalSystem.GetBlockWithName("Ejector Back") as IMyShipConnector);

    foreach (IMyShipConnector ejector in ejectors)
    {
        ejector.CollectAll = true;
        ejector.ThrowOut = true;
    }

    // Connectors.
    connectors = new List<IMyShipConnector>();
    connectors.Add(GridTerminalSystem.GetBlockWithName("Connector Front") as IMyShipConnector);
    connectors.Add(GridTerminalSystem.GetBlockWithName("Connector Back") as IMyShipConnector);

    // Battery.
    battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock;
    maxStoredPower = battery.MaxStoredPower;

    // Containers.
    containers = new List<IMyInventory>();
    containers.Add((GridTerminalSystem.GetBlockWithName("Container Front") as IMyCargoContainer).GetInventory());
    containers.Add((GridTerminalSystem.GetBlockWithName("Container Back") as IMyCargoContainer).GetInventory());

    // Initial settings.
    target = Target.Load;
    targetPosition = load;
    frontLight.Color = new Color(255, 255, 255);
    backLight.Color = new Color(255, 0, 0);
    
    ConnectorsDisabled();
    LightsEnabled();

    // ChangeDestination();

    targetVelocity = maxVelocity;
    //InvertPropulsion();
    ToggleWheels(true);
}
#endregion

public void Main(string argument, UpdateType updateSource)
{
    velocity = remoteControl.GetShipVelocities().LinearVelocity.Length();

    Echo("Target: " + target + " (" + DistanceToTarget().ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "m)");
    Echo("Phase: " + phase.ToString());
    Echo("Target speed: " + (targetVelocity * 3600 / 1000).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "km/h");
    string v = velocity.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    string s = (velocity * 3600 / 1000).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    Echo("Speed: " + s + "km/h (" + v + "m/s)");

    #region Speed regulation
    if (phase != Phase.Docked)
    {
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
    #endregion

    #region Phases
    // Crusing phase.
    if (DistanceToTarget() < startToSlowDown && phase == Phase.Crusing)
    {
        phase = Phase.Deceleration;
        targetVelocity = maxVelocity;
    }

     // Deceleration phase.
    else if (phase == Phase.Deceleration)
    {
        // If the vehicle slows down below the docking speed limit,
        // we move to the next phase.
        if (velocity < dockingVelocity)
        {
            targetVelocity = dockingVelocity;
            ConnectorsEnabled();
            phase = Phase.Docking;
            
            return;
        }

        // Otherwise, we set the target velocity based on the deceleration progression.
        targetVelocity = DecelerationStatus() * maxVelocity;
    }

    // Docking phase.
    else if (phase == Phase.Docking)
    {
        if (IsConnectable())
        {
            Connect();
            ToggleWheels(false);
            LightsDisabled();

            phase = Phase.Docked;
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }
    }

    // Docked phase.
    else if (phase == Phase.Docked)
    {
        // Check for last change in cargo volume.
        float currentVolume = ContainersCurrentVolume();

        if (currentVolume != containersLastVolume)
        {
            containersLastVolume = currentVolume;
            loadingTimeout = DateTime.Now.AddSeconds(LoadingTimeout);
        }

        // Do the work depending on the station the train is at.
        DoWork();

        // Once the work is done.
        if (CanGo()) {
            Disconnect();
            ConnectorsDisabled();
            EjectorsDisabled();
            
            targetVelocity = maxVelocity;

            ChangeDestination();
            InvertPropulsion();
            InvertLights();
            LightsEnabled();
            ToggleWheels(true);

            phase = Phase.Crusing;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
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
    target = target == Target.Load ? Target.Unload : Target.Load;
    targetPosition = target == Target.Load ? load : unload;
}

private double DecelerationStatus()
{
    return (DistanceToTarget() - connectorSafeDistance) / (startToSlowDown - connectorSafeDistance);
}

private bool IsCharged()
{
    return battery.CurrentStoredPower / maxStoredPower > operationalCharge;
}

private bool IsFull()
{
    return containers.Count((IMyInventory c) => c.IsFull) == containers.Count;
}

private bool IsEmpty()
{
    return containers.Count((IMyInventory c) => c.CurrentVolume == 0) == containers.Count;
}

private bool IsNotEmpty()
{
    return containers.Sum(delegate(IMyInventory i) { return (float)i.CurrentVolume; }) > 0f;
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
    frontLight.Color = color;
}

// Containers.
private float ContainersCurrentVolume()
{
    return containers.Sum((IMyInventory i) => (float)i.CurrentVolume);
}

// Connectors.
private void ConnectorsEnabled()
{
    foreach (IMyShipConnector connector in connectors)
    {
        connector.Enabled = true;
    }
}

private void ConnectorsDisabled()
{
    foreach (IMyShipConnector connector in connectors)
    {
        connector.Enabled = false;
    }
}

private void Connect()
{
    connectors.ForEach((IMyShipConnector c) => c.Connect());
}

private void Disconnect()
{
    connectors.ForEach((IMyShipConnector c) => c.Disconnect());
}

private bool IsConnectable()
{
    foreach (IMyShipConnector connector in connectors)
    {
        if (connector.Status == MyShipConnectorStatus.Connectable)
        {
            return true;
        }
    }

    return false;
}

private bool IsConnected()
{
    foreach (IMyShipConnector connector in connectors)
    {
        if (connector.Status == MyShipConnectorStatus.Connected)
        {
            return true;
        }
    }

    return false;
}

// Ejectors.
private void EjectorsEnabled()
{
    ejectors.ForEach((IMyShipConnector e) => e.Enabled = true);
}

private void EjectorsDisabled()
{
    ejectors.ForEach((IMyShipConnector e) => e.Enabled = false);
}

private void DoWork()
{
    if (target == Target.Unload)
    {
        EjectorsEnabled();
    }
}

private bool CanGo()
{
    if (target == Target.Load)
    {
        return (IsCharged() && IsFull()) || (IsNotEmpty() && DateTime.Now > loadingTimeout);
    }
    
    else
    {
        return IsCharged() && IsEmpty();
    }
}
