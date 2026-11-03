namespace BlinkCommon.Interfaces.System;

public interface ILocalStorageManifestResult
{
    long Manifest_Id { get; }
    List<ILocalStorageClip>? Clips { get; }
}
