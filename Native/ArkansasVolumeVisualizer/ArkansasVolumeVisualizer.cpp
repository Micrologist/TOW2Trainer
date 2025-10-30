#include <Windows.h>
#include "SDK/Engine_classes.hpp"

static inline void ProcessActor(SDK::AActor* Actor, bool visibilityState)
{
    if (!Actor) return;

    const bool bIsBlocking = Actor->IsA(SDK::ABlockingVolume::StaticClass());
    const bool bIsKillZ = Actor->IsA(SDK::AKillZVolume::StaticClass());
    if (!bIsBlocking && !bIsKillZ) return;

    SDK::UBrushComponent* BrushComponent = nullptr;

    if (bIsBlocking)
    {
        auto* BV = static_cast<SDK::ABlockingVolume*>(Actor);
        BV->BrushColor = SDK::FColor(0, 255, 0, 255);   // green
        BV->bColored = true;
        BrushComponent = BV->BrushComponent;

        // These are not for blocking us, probably
        if (BrushComponent && BrushComponent->BodyInstance.ObjectType > SDK::ECollisionChannel::ECC_Pawn)
            return;
    }
    else // KillZ
    {
        auto* KV = static_cast<SDK::AKillZVolume*>(Actor);
        KV->BrushColor = SDK::FColor(0, 0, 255, 255);
        KV->bColored = true;
        BrushComponent = KV->BrushComponent;
    }

    // Unhide actor and brush
    Actor->SetActorHiddenInGame(!visibilityState);

    if (BrushComponent)
    {
        BrushComponent->SetHiddenInGame(!visibilityState, true);
    }
}

static inline void ProcessWorld(SDK::UWorld* World, bool visibilityState)
{
    if (!World) return;

    for (SDK::ULevel* Level : World->Levels)
    {
        if (!Level) continue;
        auto& Actors = Level->Actors;
        for (SDK::AActor* Actor : Actors)
        {
            if (!Actor) continue;
            ProcessActor(Actor, visibilityState);
        }
    }
}

extern "C" __declspec(dllexport) void __stdcall RefreshVolumes(bool visibilityState)
{
    SDK::UWorld* World = SDK::UWorld::GetWorld();
    if (!World) return;
    ProcessWorld(World, visibilityState);
}

// Thread-proc thunk so you can use CreateRemoteThread and pass the bool in WPARAM-style:
// pass param == IntPtr.Zero => false, anything else => true
extern "C" __declspec(dllexport) DWORD WINAPI RefreshVolumes_Thread(LPVOID param)
{
    RefreshVolumes(param != nullptr);
    return 0;
}

// Keep DllMain minimal; do NOT run work here. Just disable attach/detach callbacks.
BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
        DisableThreadLibraryCalls(hModule);
    return TRUE;
}