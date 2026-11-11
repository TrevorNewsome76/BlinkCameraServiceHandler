using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall;

internal enum DeviceMediaKind { Current, Cloud, Local, Error }

internal record DeviceMediaEntry(string Name, DeviceMediaKind Kind, DateTime? Timestamp, ICloudMediaItem? CloudItem, ILocalStorageClip? LocalClip, IHomeScreenSyncModule? SyncModule, long? ManifestId);

internal record DeviceMediaListing(List<DeviceMediaEntry> Entries);
