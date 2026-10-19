// K3N: roster character, id 16. Powerup. Design: Design/Characters/K3N.md
// An arcade-style brawler with a pet, Zeep Zorp (only seen in his block animation for now).
//
//   Light (J)      Cape whip: a jab with a little more reach.
//   Heavy (K)      Charged Punch: hold K to charge, let go to punch. 12 -> 20 damage over 60 ticks.
//                  He steps forward a little just before the punch lands.
//   Grab           Hand grab: the standard held grab, slower (tick 17, 10 active)
//   Dashes         quick and short: forward hop 1.5 units, back leap 1.6 units
//   Special (I)    Rapid Punches: hold I to keep punching. Each pass is a triple jab (3 hitboxes, 1 damage each); 3 passes tapped, up to 10.
//   O button       Install (perk 1, D-48). NOT a normal Ultimate: a fresh press of O turns it on
//                  with ANY meter (no superflash), it drains 2 meter a tick, and O again turns it
//                  off. While it's on: +30% damage, no move cooldowns, no meter gain.
//   Perk 2         Second Wind: his cooldowns shrink as he loses health (up to -50%).
public class K3N : BaseFighter
{
    public const int Id = 16;

    public const int InstallDrainPerTick = 2;      // a full bar lasts about 8 seconds
    public const int InstallDamagePercent = 130;
    public const int InstallAttackDrain = 150;     // every Install attack (Light, Heavy or Special) also costs this much bar (1000 = full)
    public const int PunchFullCharge = 60;
    // Charged Punch steps him forward: LungeSpeed milli-units a tick for the LungeTicks ticks just
    // before the punch comes out (8 x 50 = 0.4 units). Only after he lets go of the charge.
    public const int LungeTicks = 8;
    public const int LungeSpeed = 50;

    public static readonly MoveDef CapeWhip = MakeCapeWhip();

    static MoveDef MakeCapeWhip()
    {
        return Melee("Light",
            totalTicks: 16, spawnTick: 8, activeTicks: 4,
            box: new Box(400, 1000, 1600, 500),
            damage: 5, hitstun: 12, knockback: 40, cooldown: LightCooldown);
    }

    // Grab with hand: the standard held grab (damage 12, hitstun 40, 20-tick hold), but slower to
    // start (tick 17) and open for 10 ticks, over a box that starts a little further out.
    public static readonly MoveDef HandGrab = HoldGrab(Grab("Grab",
        totalTicks: 40, spawnTick: 17, activeTicks: 10,
        box: new Box(400, 700, 700, 1000),
        damage: 12, hitstun: 40, knockback: 90, cooldown: GrabCooldown));

    public static readonly MoveDef ChargedPunch = MakeChargedPunch();

    // The three attacks he uses while Install is on. They start as copies of the normal Light, Heavy and
    // Special (same numbers) so you can tune them on their own in the Character Preview: they are the LAST
    // THREE moves in its list. They keep the move names "Light", "Heavy" and "Special", so the Install_Light /
    // Install_Heavy / Install_Special animation states (when you make them), the sounds and the flashes all match.
    public static readonly MoveDef InstallWhip = MakeCapeWhip();
    public static readonly MoveDef InstallPunch = MakeChargedPunch();
    public static readonly MoveDef RapidPunches = MakeRapidPunches();
    public static readonly MoveDef InstallSpecial = MakeRapidPunches(true);

    static MoveDef MakeChargedPunch()
    {
        MoveDef m = Melee("Heavy",
            totalTicks: 28, spawnTick: 20, activeTicks: 7,
            box: new Box(300, 900, 4000, 500),
            damage: 12, hitstun: 20, knockback: 80, cooldown: HeavyCooldown);
        m.holdButton = Buttons.Heavy;
        m.chargeTick = 0;
        m.chargeMax = PunchFullCharge;
        return m;
    }

    // Ticks 0-5 wind up, then ticks 6-10 (one punch each pass) repeat while I is held, then 14
    // ticks of recovery. Hitstun 10 is longer than the 5 ticks between punches, so a string that
    // hits keeps hitting. Knockback is tiny (3 x 10 ticks = 0.03 units a hit) so ten punches don't
    // push the victim out of reach.
    //
    // Each pass spawns THREE hitboxes (ticks 7, 10 and 21 of the move, different boxes), 1 damage
    // each, so a pass is 3 damage. NOTE: the loop only repeats ticks 6-10, so the tick-21 box
    // fires once, after the loop ends.
    static MoveDef MakeRapidPunches() { return MakeRapidPunches(false); }

    // The Install version has FIVE hitboxes (tune them in the Character Preview: "Special (INSTALL)").
    static MoveDef MakeRapidPunches(bool install)
    {
        MoveDef m = Melee("Special",
            totalTicks: 25, spawnTick: 6, activeTicks: 2,
            box: new Box(300, 800, 900, 600),
            damage: 1, hitstun: 10, knockback: 1, cooldown: SpecialCooldown);
        HitboxDef first = m.hitboxes[0];
        // Three hitboxes (tuned in the Character Preview): a short low-ish jab, a tall wide burst, and a long finisher.
        int[] spawn = { 7, 10, 21 };
        int[] life = { 2, 6, 2 };
        Box[] boxes = { new Box(500, 1000, 1600, 600), new Box(300, 0, 2000, 2000), new Box(300, 1000, 2600, 700) };
        if (install)
        {
            // the same three plus a high jab (tick 9) and a long low finisher (tick 23)
            spawn = new[] { 7, 9, 10, 21, 23 };
            life = new[] { 2, 2, 6, 2, 2 };
            boxes = new[] { new Box(500, 1000, 1600, 600), new Box(400, 1300, 1500, 500), new Box(300, 0, 2000, 2000), new Box(300, 1000, 2600, 700), new Box(300, 800, 3400, 900) };
        }
        m.hitboxes = new HitboxDef[spawn.Length];
        for (int i = 0; i < spawn.Length; i++)
        {
            m.hitboxes[i] = new HitboxDef
            {
                spawnTick = spawn[i], lifetime = life[i], box = boxes[i],
                damage = first.damage, hitstun = first.hitstun, knockback = first.knockback,
                followOwner = true,
            };
        }
        m.holdButton = Buttons.Special;
        m.loopStart = 6;
        m.loopEnd = 10;
        m.loopMin = 3;
        m.loopMax = 10;
        return m;
    }

    public bool installOn;

    protected override MoveDef LightAttack { get { return installOn ? InstallWhip : CapeWhip; } }
    protected override MoveDef HeavyAttack { get { return installOn ? InstallPunch : ChargedPunch; } }
    protected override MoveDef GrabMove { get { return HandGrab; } }
    protected override MoveDef SpecialMove { get { return installOn ? InstallSpecial : RapidPunches; } }
    protected override MoveDef UltimateMove { get { return null; } }   // O is the Install toggle

    public override bool IsPoweredMove(MoveDef m) { return m == InstallWhip || m == InstallPunch || m == InstallSpecial; }

    // Editor tools list the Install attacks too (last three).
    public override MoveDef[] ListMoves()
    {
        MoveDef[] normal = base.ListMoves();
        MoveDef[] all = new MoveDef[normal.Length + 3];
        normal.CopyTo(all, 0);
        all[normal.Length] = InstallWhip;
        all[normal.Length + 1] = InstallPunch;
        all[normal.Length + 2] = InstallSpecial;
        return all;
    }

    public override DashDef ForwardDash { get { return new DashDef(10, 150); } }
    public override DashDef BackDash { get { return new DashDef(12, 130); } }

    // Every Install attack costs a big chunk of the bar on top of the steady drain. Running dry ends Install.
    public override void OnMoveStart(GameSim sim)
    {
        if (!installOn) return;
        if (move != InstallWhip && move != InstallPunch && move != InstallSpecial) return;
        AddMeter(-InstallAttackDrain);
        if (meter == 0) installOn = false;
    }

    public override void OnTick(GameSim sim)
    {
        // A knocked-out K3N switches Install off, so the bar stops draining once he is dead.
        if (hp <= 0)
        {
            installOn = false;
            return;
        }

        if ((pressedInput & Buttons.Ultimate) != 0 && CanToggleInstall)
        {
            if (installOn)
            {
                installOn = false;
                sim.AddEvent(SimEventType.PowerUp, index, -1, x, y, 0);
            }
            else if (meter > 0)
            {
                installOn = true;
                ClearCooldowns();   // "no move cooldowns" includes the ones already running
                sim.AddEvent(SimEventType.PowerUp, index, -1, x, y, 1);
            }
        }

        if (installOn)
        {
            AddMeter(-InstallDrainPerTick);
            if (meter == 0) installOn = false;
        }
    }

    // He can flip it whenever he could act: not while stunned, held or knocked out.
    bool CanToggleInstall
    {
        get
        {
            return state == FighterStateId.Idle || state == FighterStateId.Walk || state == FighterStateId.Attack
                || state == FighterStateId.Dash || state == FighterStateId.Jump;
        }
    }

    public override bool CanGainMeter { get { return !installOn; } }

    public override int ModifyCooldown(MoveDef ended, int ticks)
    {
        if (installOn) return 0;
        return ticks * (100 - MissingHpPercent / 2) / 100;   // Second Wind
    }

    // The Charged Punch's forward step (both the normal and the Install version). Timed from the
    // punch's own spawn tick, so it stays lined up if the punch is retuned in the Character Preview.
    // Bodies still can't pass through each other, so he stops at the opponent.
    public override void OnMoveTick(GameSim sim)
    {
        if (!IsDoing(ChargedPunch) && !IsDoing(InstallPunch)) return;
        int punchTick = move.hitboxes[0].spawnTick;
        bool lunging = stateTick >= punchTick - LungeTicks && stateTick < punchTick;
        vx = lunging ? facing * LungeSpeed : 0;
    }

    public override void OnHitboxSpawned(GameSim sim, HitboxInstance hit)
    {
        if (hit.def == ChargedPunch.hitboxes[0] || hit.def == InstallPunch.hitboxes[0])
        {
            hit.damage = 12 + 8 * chargeTicks / PunchFullCharge;
            hit.hitstun = 20 + 6 * chargeTicks / PunchFullCharge;
            hit.knockback = 80 + 40 * chargeTicks / PunchFullCharge;
        }
    }

    public override void ModifyOutgoingHit(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        if (installOn) hit.damage = hit.damage * InstallDamagePercent / 100;
    }

    public override bool UltimateIsToggle { get { return true; } }   // AI hint

    public override string HudInfo { get { return installOn ? "INSTALL" : null; } }
    public override bool IsPoweredUp { get { return installOn; } }

    public override int HashExtra() { return installOn ? 1 : 0; }
}
