namespace Afney.Cad.Mechanical.Enums;

/*
   NE: Mekanik Nesne Tipleri (MechanicalEntityType)
   NEDEN: Nesnelerin AutoCAD ve BIM standartlarındaki kimliklerini (Identity) belirlemek için.
*/
public enum MechanicalEntityType
{
    Undefined = 0,
    Pipe = 1,
    Valve = 2,
    Pump = 3,
    SanitaryFixture = 4,
    MechanicalEquipment = 5,
    Duct = 6,
    Accessory = 7,
    Room = 8,
    AirTerminal = 9,
    Damper = 10
}
