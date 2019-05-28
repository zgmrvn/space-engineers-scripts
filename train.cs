private Vector3D start = new Vector3(-51677.18, -27949.92, 16191.69);
private Vector3D end = new Vector3(-51661.19, -27989.10, 16175.97);
private const float startToSlowDown = 20f;
private const float threshold = 10f;
private const float maxSpeed = 10f;

private bool run = false;
private List<IMyTerminalBlock> wheels;
private IMyReflectorLight frontLight;
private IMyRadioAntenna antenna;
private string target;
private Vector3D targetPosition;

private int tempCounter = 0;

private enum Phase
{
    Crusing,
    Deceleration,
    Docking,
    Docked
}

private Phase phase = Phase.Crusing;

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
    frontLight = GridTerminalSystem.GetBlockWithName("FrontLight") as IMyReflectorLight;


    // Set initial target.
    target = "End";
    targetPosition = end;

    // ChangeDestination();
    InvertPropulsion();
    ToggleWheels(true);
    //frontLight.ApplyAction("OnOff_On");
}

public void Main(string argument, UpdateType updateSource)
{
    Echo(target);
    Echo(DistanceToTarget().ToString());
    Echo(phase.ToString());
    Echo(Me.ToString());

    if (tempCounter > 1)
    {
        run = false;
    }

    if (!run)
    {
        ToggleWheels(false);
        return;
    }

    // From Crusing to Deceleration phase.
    if (DistanceToTarget() < startToSlowDown && phase == Phase.Crusing)
    {
        phase = Phase.Deceleration;

        ToggleWheels(false);
    }

    else if (phase == Phase.Deceleration)
    {
        Echo(DecelerationStatus().ToString());
    }

    // From Deceleration to Docking phase.
    else if (DistanceToTarget() < threshold && phase == Phase.Deceleration)
    {
        phase = Phase.Docking;

        ChangeDestination();
        InvertPropulsion();

        tempCounter++;
    }

    // From Docking to Docked phase.
    // else if (DistanceToTarget() < threshold && phase == Phase.Deceleration)
    // {
    //     phase = Phase.Docked;
    //     ChangeDestination();
    //     InvertPropulsion();
    //     tempCounter++;
    // }
}

private void ToggleWheels(bool state)
{
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
    ToggleWheels(false);

    foreach (IMyMotorSuspension wheel in wheels)
    {
        wheel.Brake = state;
    } 
}

private void ChangeDestination()
{
    target = target == "End" ? "Start" : "End";
    targetPosition = target == "End" ? end : start;
}

private double DecelerationStatus()
{
    return (DistanceToTarget() - (startToSlowDown - threshold)) / (startToSlowDown - threshold);
}


/* This program tests the new structures exposed in IMyShipController on Thursday, May 19, 2016.
   Space Engineers version 01.135.004 

   The exposed variables are courtesy of Lord Devious, a.k.a. Malware.
   
   This program written by Seamus Donohue of EVE University, Saturady, May 21st, 2016.

   To use this program, set up a Timer Block set to Trigger Now on itself and run this program. */
 
// string outputblockstring = "Text Panel 1";
// string remotecontrolstring = "Remote Control 1";
 
// void Main(string argument) { 
//     IMyTerminalBlock rcblock = GridTerminalSystem.GetBlockWithName(remotecontrolstring);
//     IMyTextPanel output = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(outputblockstring); 
 
//     if (rcblock == null) throw new Exception("\n\nRemote Control not found by name, check string constants."); 
//     if (output == null) throw new Exception("\n\nText Panel not found by name, check string constants."); 
 
//     IMyShipController rc = (IMyShipController)rcblock;
 
//     Vector3D rcposition = rc.GetPosition();

//     MyShipVelocities velocitystats = rc.GetShipVelocities();
//     MyShipMass massstats = rc.CalculateShipMass();

//     int shipmass = massstats.BaseMass;
//     int totalmass = massstats.TotalMass;
//     Vector3D velocity = velocitystats.LinearVelocity;
//     Vector3D rotation = velocitystats.AngularVelocity;
 
//     output.WritePublicText("Position:\n" + rcposition.X + "\n" + rcposition.Y + "\n" + rcposition.Z + "\n"
//                          + "\nshipmass:" + shipmass + "\ntotalmass:" + totalmass
//                          + "\ncargo:" + (totalmass-shipmass)
//                          + "\nLinVel:\n" + velocity.X + "\n" + velocity.Y + "\n" + velocity.Z + "\n"
//                          + "\nAngVel:\n" + rotation.X + "\n" + rotation.Y + "\n" + rotation.Z + "\n");
// }