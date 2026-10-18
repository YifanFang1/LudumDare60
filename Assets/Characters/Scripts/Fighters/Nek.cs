// NEK (temp name): roster character, id 16. Powerup. Design: Design/Characters/Nek.md
// An arcade-style brawler with a pet, Zeep Zorp (only seen in his block animation for now).
//
//   Light (J)      Cape whip: a jab with a little more reach.
//   Heavy (K)      Charged Punch: hold K to charge, let go to punch. 12 -> 20 damage over 60 ticks.
//   Grab           standard
//   Dashes         quick and short: forward hop 1.5 units, back leap 1.6 units
//   Special (I)    Rapid Punches: hold I to keep punching (2 damage each), 3 hits tapped, up to 10.
//   O button       Install (perk 1, D-48). NOT a normal Ultimate: a fresh press of O turns it on
//                  with ANY meter (no superflash), it drains 2 meter a tick, and O again turns it
//                  off. While it's on: +30% damage, no move cooldowns, no meter gain.
//   Perk 2         Second Wind: his cooldowns shrink as he loses health (up to -50%).
public class Nek : BaseFighter
{
    public const int Id = 16;

    public const int InstallDrainPerTick = 2;      // a full bar lasts about 8 seconds
    public const int InstallDamagePercent = 130;
    public const int PunchFullCharge = 60;

    public static readonly MoveDef CapeWhip = Melee("Light",
        totalTicks: 16, spawnTick: 5, activeTicks: 3,
        box: new Box(400, 900, 1200, 400),
        damage: 5, hitstun: 12, knockback: 40, cooldown: LightCooldown);

    public static readonly MoveDef ChargedPunch = MakeChargedPunch();
    public static readonly MoveDef RapidPunches = MakeRapidPunches();

    static MoveDef MakeChargedPunch()
    {
        MoveDef m = Melee("Heavy",
            totalTicks: 28, spawnTick: 10, activeTicks: 4,
            box: new Box(300, 700, 1300, 700),
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
    static MoveDef MakeRapidPunches()
    {
        MoveDef m = Melee("Special",
            totalTicks: 25, spawnTick: 6, activeTicks: 3,
            box: new Box(300, 800, 900, 600),
            damage: 2, hitstun: 10, knockback: 3, cooldown: SpecialCooldown);
        m.holdButton = Buttons.Special;
        m.loopStart = 6;
        m.loopEnd = 10;
        m.loopMin = 3;
        m.loopMax = 10;
        return m;
    }

    public bool installOn;

    protected override MoveDef LightAttack { get { return CapeWhip; } }
    protected override MoveDef HeavyAttack { get { return ChargedPunch; } }
    protected override MoveDef SpecialMove { get { return RapidPunches; } }
    protected override MoveDef UltimateMove { get { return null; } }   // O is the Install toggle

    public override DashDef ForwardDash { get { return new DashDef(10, 150); } }
    public override DashDef BackDash { get { return new DashDef(12, 130); } }

    public override void OnTick(GameSim sim)
    {
        if ((pressedInput & Buttons.Ultimate) != 0 && CanToggleInstall)
        {
            if (installOn) installOn = false;
            else if (meter > 0)
            {
                installOn = true;
                ClearCooldowns();   // "no move cooldowns" includes the ones already running
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

    public override void OnHitboxSpawned(GameSim sim, HitboxInstance hit)
    {
        if (hit.def == ChargedPunch.hitboxes[0])
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
