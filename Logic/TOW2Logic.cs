using System;
using System.Diagnostics;
using System.Threading;

namespace TOW2Trainer.Logic
{
    internal class TOW2Logic
    {
        public bool ShouldAbort { get; set; }
        public bool ShouldNoclip { get; set; }
        public bool ShouldGod { get; set; }
        public bool ShouldAmmo { get; set; }
        public bool ShouldStore { get; set; }
        public bool ShouldTeleport { get; set; }
        

        public float FlySpeedMult { get; set; } = 1f;

        public double XPos { get; private set; }
        public double YPos { get; private set; }
        public double ZPos { get; private set; }
        public double Vel { get; private set; }

        private readonly TOW2Memory mem;

        private readonly double[] storedPos = new double[5];

        private bool showingVolumes;

        private IntPtr cachedPlayerPtr;

        public TOW2Logic()
        {
            mem = new TOW2Memory();
            ShouldAbort = false;
            Task.Run(UpdateAsync);
        }

        private async Task UpdateAsync()
        {
            while (!ShouldAbort)
            {
                if (mem.UpdateState())
                {
                    UpdateUIValues();
                    SetGameState();

                    if ((IntPtr)mem.Watchers["playerCharacter"].Current != cachedPlayerPtr)
                    {
                        cachedPlayerPtr = (IntPtr)mem.Watchers["playerCharacter"].Current;
                        showingVolumes = false;
                    }
                }
                await Task.Delay(16);
            }
        }

        private void SetGameState()
        {
            var canBeDamaged = IsBitSet((byte)mem.Watchers["canBeDamaged"].Current, 2);
            var cheatFlying = IsBitSet((byte)mem.Watchers["cheatFlying"].Current, 4);
            var movementMode = (byte)mem.Watchers["movementMode"].Current;
            var acceleration = (float)mem.Watchers["acceleration"].Current;
            var flySpeed = (float)mem.Watchers["flySpeed"].Current;

            if (ShouldStore)
            {
                ShouldStore = false;
                StorePosition();
            }

            if (ShouldTeleport)
            {
                ShouldTeleport = false;
                Teleport();
            }

            if (!ShouldNoclip && canBeDamaged == ShouldGod)
            {
                SetGod(ShouldGod);
            }

            if (cheatFlying != ShouldNoclip || ShouldNoclip && (movementMode != 5 || acceleration != 99999f || flySpeed != 2500 * FlySpeedMult))
            {
                SetNoclip(ShouldNoclip);
            }
        }

        private void UpdateUIValues()
        {
            XPos = (double)mem.Watchers["xPos"].Current;
            YPos = (double)mem.Watchers["yPos"].Current;
            ZPos = (double)mem.Watchers["zPos"].Current;
            double xVel = (double)mem.Watchers["xVel"].Current;
            double yVel = (double)mem.Watchers["yVel"].Current;
            double hVel = Math.Floor(Math.Sqrt(xVel * xVel + yVel * yVel) + 0.5f) / 100;
            Vel = (double)hVel;
        }

        private void StorePosition()
        {
            storedPos[0] = (double)mem.Watchers["xPos"].Current;
            storedPos[1] = (double)mem.Watchers["yPos"].Current;
            storedPos[2] = (double)mem.Watchers["zPos"].Current;
            storedPos[3] = (double)mem.Watchers["vLook"].Current;
            storedPos[4] = (double)mem.Watchers["hLook"].Current;
        }

        private void Teleport()
        {
            mem.Write("xPos", storedPos[0]);
            mem.Write("yPos", storedPos[1]);
            mem.Write("zPos", storedPos[2]);
            mem.Write("vLook", storedPos[3]);
            mem.Write("hLook", storedPos[4]);
        }

        private void SetGod(bool b)
        {
            byte canBeDamaged = (byte)mem.Watchers["canBeDamaged"].Current;
            canBeDamaged = SetBit(canBeDamaged, 2, !b);
            mem.Write("canBeDamaged", canBeDamaged);
        }

        private void SetNoclip(bool b)
        {
            byte cheatFlying = (byte)mem.Watchers["cheatFlying"].Current;
            cheatFlying = SetBit(cheatFlying, 4, b);
            byte collisionEnabled = (byte)mem.Watchers["collisionEnabled"].Current;
            collisionEnabled = SetBit(collisionEnabled, 7, !b);
            byte movementMode;
            float flySpeed, acceleration;
            bool godMode;

            if (b)
            {
                movementMode = 5;
                flySpeed = 2500 * FlySpeedMult;
                acceleration = 99999;
                godMode = true;
            }
            else
            {
                movementMode = 3;
                flySpeed = 1000;
                acceleration = 2048;
                godMode = ShouldGod;
            }
            mem.Write("cheatFlying", cheatFlying);
            mem.Write("movementMode", movementMode);
            mem.Write("flySpeed", flySpeed);
            mem.Write("acceleration", acceleration);
            mem.Write("collisionEnabled", collisionEnabled);
            mem.Write("jumpApex", float.MinValue);
            SetGod(godMode);
        }

        private static bool IsBitSet(byte b, int n)
        {
            return (b & 1 << n) != 0;
        }

        private static byte SetBit(byte b, int i, bool v)
        {
            return v ? (byte)(b | 1 << i) : (byte)(b & ~(1 << i));
        }

        internal void ToggleVolumes()
        {
            showingVolumes = !showingVolumes;
            mem.SetVolumesVisible(showingVolumes);
        }
    }
}