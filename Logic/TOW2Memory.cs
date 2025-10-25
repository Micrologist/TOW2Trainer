using System.Diagnostics;
using System.Text.RegularExpressions;
using TOW2Trainer.MemUtil;

namespace TOW2Trainer.Logic
{
    internal class TOW2Memory
    {
        public MemoryWatcherList? Watchers { get; private set; }
        public bool IsInitialized { get; private set; } = false;

        private Process? proc;

        public bool UpdateState()
        {
            if (!IsHooked() || !IsInitialized)
            {
                IsInitialized = false;
                Hook();
                Thread.Sleep(1000);
                return false;
            }

            try
            {
                Watchers.UpdateAll(proc);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        private bool IsHooked()
        {
            return proc != null && !proc.HasExited;
        }

        private void Hook()
        {
            List<Process> processList = Process.GetProcesses().ToList().FindAll(x => Regex.IsMatch(x.ProcessName, "TheOuterWorlds2.*-Shipping"));
            if (processList.Count == 0)
            {
                proc = null;
                return;
            }
            proc = processList[0];

            if (IsHooked())
            {
                IsInitialized = Initialize();
            }
        }

        private bool Initialize()
        {

            nint localPlayerPtr;
            try
            {
                SignatureScanner scanner = new SignatureScanner(proc, proc.MainModule.BaseAddress, proc.MainModule.ModuleMemorySize);
                localPlayerPtr = GetLocalPlayerPtr();
                if (localPlayerPtr == nint.Zero)
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            const int OFFSET_CONTROLLER = 0x30;
            const int OFFSET_CHARACTER = 0x378;
            const int OFFSET_CAPSULE = 0x3C8;
            const int OFFSET_MOVEMENT = 0x3C0;
            Debug.WriteLine(localPlayerPtr.ToString("X8"));

            Watchers = [
                // LocalPlayer -> PlayerController -> PlayerCharacter -> PlayerCapsule -> Position
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_CAPSULE, 0x1A0)) { Name = "xPos" },
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_CAPSULE, 0x1A8)) { Name = "yPos" },
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_CAPSULE, 0x1B0)) { Name = "zPos" },

                // LocalPlayer -> PlayerController -> PlayerCharacter -> PlayerMovement -> Velocity
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_MOVEMENT, 0x130)) { Name = "xVel" },
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_MOVEMENT, 0x138)) { Name = "yVel" },
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_MOVEMENT, 0x140)) { Name = "zVel" },

                // LocalPlayer -> PlayerController -> PlayerCharacter -> PlayerMovement -> MovementMode
                new MemoryWatcher<byte>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_MOVEMENT, 0x291)) { Name = "movementMode" },

                // LocalPlayer -> PlayerController -> PlayerCharacter -> PlayerMovement -> MaxFlySpeed
                new MemoryWatcher<float>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_MOVEMENT, 0x2E4)) { Name = "flySpeed" },

                // LocalPlayer -> PlayerController -> PlayerCharacter -> PlayerMovement -> MaxAcceleration
                new MemoryWatcher<float>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_MOVEMENT, 0x2EC)) { Name = "acceleration" },

                // LocalPlayer -> PlayerController -> PlayerCharacter -> PlayerMovement -> CheatFlying
                new MemoryWatcher<byte>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, OFFSET_MOVEMENT, 0x5AD)) { Name = "cheatFlying" },

                // LocalPlayer -> PlayerController -> PlayerCharacter -> EnableCollision
                new MemoryWatcher<byte>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, 0xBD)) { Name = "collisionEnabled" },

                // LocalPlayer -> PlayerController -> PlayerCharacter -> CanBeDamaged
                new MemoryWatcher<byte>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, OFFSET_CHARACTER, 0xBA)) { Name = "canBeDamaged" },

                // LocalPlayer -> PlayerController -> ControlRotation
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, 0x3A0)) { Name = "vLook" },
                new MemoryWatcher<double>(new DeepPointer(localPlayerPtr, OFFSET_CONTROLLER, 0x3A8)) { Name = "hLook" },
            ];


            try
            {
                Watchers.UpdateAll(proc);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        public void Write(string name, byte[] bytes)
        {
            if (!IsHooked() || !IsInitialized || !Watchers[name].DeepPtr.DerefOffsets(proc, out nint addr))
            {
                return;
            }

            try
            {
                _ = proc.WriteBytes(addr, bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void Write(string name, float fValue)
        {
            Write(name, BitConverter.GetBytes(fValue));
        }

        public void Write(string name, double dValue)
        {
            Write(name, BitConverter.GetBytes(dValue));
        }

        public void Write(string name, bool boolValue)
        {
            Write(name, BitConverter.GetBytes(boolValue));
        }

        public void Write(string name, byte bValue)
        {
            Write(name, new byte[] { bValue });
        }

        private nint GetLocalPlayerPtr()
        {
            var scn = new SignatureScanner(proc, proc.MainModule.BaseAddress, proc.MainModule.ModuleMemorySize);

            var gameEngineTrg = new SigScanTarget(8, "E8 ?? ?? ?? ?? 48 39 35 ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 8B 0D") { OnFound = (p, s, ptr) => ptr + 0x4 + proc.ReadValue<int>(ptr) };
            var gameEnginePtr = scn.Scan(gameEngineTrg);

            var localPlayerPtr = new DeepPointer(gameEnginePtr, 0x1178, 0x38).Deref<IntPtr>(proc);

            return localPlayerPtr;
        }

    }
}