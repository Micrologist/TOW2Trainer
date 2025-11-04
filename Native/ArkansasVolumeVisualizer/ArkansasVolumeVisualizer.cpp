#include <Windows.h>
#include <fstream>
#include <string>
#include <type_traits>
#include "SDK/Engine_classes.hpp"
#include "SDK/Arkansas_classes.hpp"
#include <functional>

static void RefreshVolumes_Impl(bool visible)
{
    auto* World = SDK::UWorld::GetWorld();
    if (!World) return;

    SDK::TArray<SDK::AActor*> volumes;
    SDK::UGameplayStatics::GetAllActorsOfClass(World, SDK::AVolume::StaticClass(), &volumes);

    for (auto* actor : volumes)
    {
        if (!actor) continue;

        const bool isBV = actor->IsA(SDK::ABlockingVolume::StaticClass());
        const bool isKZ = actor->IsA(SDK::AKillZVolume::StaticClass());
        if (!isBV && !isKZ) continue;

        SDK::UBrushComponent* brush = nullptr;
        if (isBV)
        {
            auto* bv = static_cast<SDK::ABlockingVolume*>(actor);
            bv->BrushColor = SDK::FColor(0, 255, 0, 255);
            bv->bColored = true;
            brush = bv->BrushComponent;

            if (brush && brush->BodyInstance.ObjectType > SDK::ECollisionChannel::ECC_Pawn)
                continue;
        }
        else
        {
            auto* kz = static_cast<SDK::AKillZVolume*>(actor);
            kz->BrushColor = SDK::FColor(0, 0, 255, 255);
            kz->bColored = true;
            brush = kz->BrushComponent;
        }

        actor->SetActorHiddenInGame(!visible);
        if (brush) brush->SetHiddenInGame(!visible, true);
    }
}

static void UnlockConsole_Impl()
{
    auto* Engine = SDK::UEngine::GetEngine();
    if (!Engine || !Engine->GameViewport || !Engine->ConsoleClass)
        return;

    SDK::UObject* NewConsoleObj =
        SDK::UGameplayStatics::SpawnObject(Engine->ConsoleClass, Engine->GameViewport);
    if (NewConsoleObj)
        Engine->GameViewport->ViewportConsole = static_cast<SDK::UConsole*>(NewConsoleObj);

    if (auto* Settings = SDK::UInputSettings::GetDefaultObj())
    {
        const auto insertName = SDK::UKismetStringLibrary::Conv_StringToName(L"Insert");
        Settings->ConsoleKeys[0].KeyName = insertName;
    }
}

extern "C" __declspec(dllexport) DWORD WINAPI RefreshVolumes_Thread(LPVOID param)
{
    const bool visible = (param != nullptr);
    RefreshVolumes_Impl(visible);
    return 0;
}

extern "C" __declspec(dllexport) DWORD WINAPI UnlockConsole_Thread(LPVOID param)
{
    UnlockConsole_Impl();
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hModule);
    }
    return TRUE;
}