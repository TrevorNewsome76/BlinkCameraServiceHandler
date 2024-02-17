namespace BlinkCommon.Interfaces.System;

public interface IGetHomeScreenResponse
{
    IHomeScreenAccount Account { get; }
    List<IHomeScreenNetwork>Networks { get; }
    List<IHomeScreenSyncModule> Sync_Modules { get; }
    List<IHomeScreenCamera> Cameras { get; }
    string[] Sirens { get; }
    string[] Chimes { get; }
    IHomeScreenVideoStats Video_Stats { get; }
    string[] Doorbell_Buttons { get; }
    string[] Owls { get; }
    List<IHomeScreenDoorbells> Doorbells { get; }
    IHomeScreenAppUpdates App_Updates { get; }
    IHomeScreenDeviceLimits Device_Limits { get; }
    IHomeScreenWhatsNew Whats_New { get; }
    IHomeScreenSubscriptions Subscriptions { get; }
    IHomeScreenEntitlements Entitlements { get; }
    bool Tiv_Lock_Enable { get; }
    IHomeScreenTivLockStatus Tiv_LockStatus { get; }
    IHomeScreenAccessories Accessories { get; }
}