namespace Sanierungsplaner.Sync;
public static class MeshSchedule
{
 public static bool IsNight(DateTime local)=>local.Hour>=22||local.Hour<3;
}
