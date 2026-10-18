using System;
using System.Collections.Generic;

// A simple opponent so the game is playable offline. It knows nothing about specific characters:
// it uses the shared kit (Design/SHARED_KIT.md) the same way for everyone.
//
//   far away     walk in, sometimes dash in, sometimes throw the Special
//   mid range    walk in, sometimes jump in and swing a Heavy on the way down
//   close        jab, heavy or grab (grabs more often when you're blocking), sometimes back dash
//   you attack   about half the time it holds back to block until your move ends
//                (never, for a character that can't block)
//   full meter   fires the Ultimate when you're in range
//   holding you  uses its hold follow-up (Special) during a held grab
//   toggles      an Ultimate button that toggles a mode (Fighter.UltimateIsToggle) goes on up close
//                with some meter, and off again when you're far away, to save the meter
//   switching    taps the Special to switch it when the fighter asks (Fighter.WantsSpecialSwitch),
//                and holds it long enough to use it (Fighter.SpecialHoldTicks)
//
// It doesn't know the characters, but it asks each fighter's own ChooseMove whether a button
// would start anything right now (ChooseMove only reads the sim, so asking is safe). That way it
// skips moves on cooldown, an empty gun or a potion that isn't ready. The Special and the Heavy
// are sometimes HELD for a while, which charges charge moves, keeps "hold to repeat" moves going
// and uses "tap vs hold" potions; a short press of the Special taps instead.
//
// It only runs on one machine, so it is allowed to read the sim and use System.Random.
// It only ever outputs button bits, exactly like a keyboard, so it can't cheat the sim.
public class AIInputSource : IInputSource
{
    const int CloseRange = 1500;    // centre to centre: Light and Grab reach about this far
    const int FarRange = 4000;
    const int UltimateRange = 2600;
    const int ToggleOnMeter = 400;

    GameSim sim;
    int me;
    Random rng = new Random(1);
    Dictionary<int, int> inputs = new Dictionary<int, int>();
    Queue<int> planned = new Queue<int>();   // inputs already decided for the next ticks (dash taps, holds)
    int blockUntilTick = -1;                 // keep holding back until this sim tick
    int lastOutput;

    public AIInputSource(GameSim sim, int myIndex)
    {
        this.sim = sim;
        this.me = myIndex;
    }

    public void Sample(int tick)
    {
        int bits = planned.Count > 0 ? planned.Dequeue() : Decide();
        inputs[tick] = bits;
        lastOutput = bits;
    }

    public bool TryGet(int tick, out int bits)
    {
        return inputs.TryGetValue(tick, out bits);
    }

    int Decide()
    {
        Fighter self = sim.fighters[me];
        Fighter foe = sim.fighters[1 - me];
        int distance = foe.x - self.x;
        int gap = Math.Abs(distance);
        int toward = distance > 0 ? Buttons.Right : Buttons.Left;
        int away = distance > 0 ? Buttons.Left : Buttons.Right;

        // In the air the arc is fixed, so the only choice is when to swing: on the way down, close in.
        if (self.state == FighterStateId.Jump)
        {
            if (!self.usedAirMove && self.vy < 0 && gap < CloseRange + 300) return Buttons.Heavy;
            return 0;
        }

        // Holding someone with a held grab: the follow-up is the whole point.
        if (self.holdTicks > 0) return Buttons.Special;

        // Block: when the player starts a move, about half the time hold back until it's over.
        if (self.CanBlock && foe.state == FighterStateId.Attack && foe.stateTick == 0 && foe.move != null && rng.Next(2) == 0)
        {
            blockUntilTick = sim.tick + foe.move.totalTicks;
        }
        if (sim.tick < blockUntilTick) return away;

        bool freshUltimate = (lastOutput & Buttons.Ultimate) == 0;
        if (self.UltimateIsToggle)
        {
            // A mode: on up close with a decent amount of meter, off when far (it drains).
            bool on = self.IsPoweredUp;
            if (freshUltimate && !on && self.meter >= ToggleOnMeter && gap < UltimateRange && rng.Next(8) == 0) return Buttons.Ultimate;
            if (freshUltimate && on && gap > FarRange && rng.Next(8) == 0) return Buttons.Ultimate;
        }
        else if (self.meter >= Fighter.MeterMax && gap < UltimateRange && freshUltimate && rng.Next(10) == 0)
        {
            // Ultimate: needs a full bar and a fresh press (so not if O was held last tick).
            return Buttons.Ultimate;
        }

        // Switch the Special to the version that suits the moment: a quick tap, then let go.
        if (self.WantsSpecialSwitch(sim) && (lastOutput & Buttons.Special) == 0 && rng.Next(10) == 0)
        {
            planned.Enqueue(0);
            return Buttons.Special;
        }

        if (gap > FarRange)
        {
            int roll = rng.Next(100);
            if (roll < 2 && Ready(self, Buttons.Special)) return PlanHold(Buttons.Special, self.SpecialHoldTicks);
            if (roll < 3) return PlanDash(toward);
            return toward;
        }

        if (gap > CloseRange)
        {
            if (rng.Next(80) == 0 && Ready(self, Buttons.Special)) return PlanHold(Buttons.Special, self.SpecialHoldTicks);
            if (gap < 3000 && rng.Next(90) == 0) return Buttons.Up | toward;   // jump in
            return toward;   // walk in
        }

        // Close: mostly wait a beat between attacks, so the player can act too.
        int r = rng.Next(40);
        if (r < 2 && Ready(self, Buttons.Light)) return Buttons.Light;
        if (r == 2 && Ready(self, Buttons.Heavy)) return PlanHold(Buttons.Heavy, 0);
        if ((r == 3 || (foe.holdingBack && r < 6)) && Ready(self, Buttons.Grab)) return Buttons.Grab;
        if (r == 5 && Ready(self, Buttons.Special)) return PlanHold(Buttons.Special, self.SpecialHoldTicks);
        // Back dashes can cost health for some characters, so not when low.
        if (r == 4 && self.hp * 4 > self.MaxHp) return PlanDash(away);
        return 0;
    }

    // Would this button start a move right now? Asks the fighter itself (read only).
    bool Ready(Fighter self, int button)
    {
        return self.ChooseMove(sim, button) != null;
    }

    // Presses `button` and keeps it held for a random 1 to 40 ticks: a tap, a short hold or a full
    // charge, but at least `minTicks` (a move that must be held to work). Returns this tick's
    // input; the rest is queued.
    int PlanHold(int button, int minTicks)
    {
        int ticks = rng.Next(4) == 0 ? 1 : rng.Next(1, 41);
        if (ticks < minTicks) ticks = minTicks;
        for (int i = 1; i < ticks; i++) planned.Enqueue(button);
        return button;
    }

    // A dash is a double-tap: release, tap, release, tap. Each tap must be a FRESH press, so the
    // key is let go first in case it was being held to walk. Returns this tick's input; the rest
    // is queued for the next ticks.
    int PlanDash(int direction)
    {
        planned.Enqueue(direction);
        planned.Enqueue(0);
        planned.Enqueue(direction);
        return 0;
    }
}
