// <copyright file="Program.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

// PvP balance simulator: builds every master class from the live game configuration (the
// database the server uses) at the same stats and gear tier, lets them hit each other through
// the server's own damage code (AttackableExtensions.CalculateDamageAsync) and reports how long
// each class needs to kill each other class.
//
//   dotnet run -c Release -- [--port 5433] [--stat 32767] [--level 400] [--item-level 15]
//                            [--samples 4000] [--duels 10] [--duel-seconds 180] [--no-potions]
//                            [--no-gear] [--no-buffs] [--out report.md]
//
// Model (see the report's notes): 3rd class characters without master skills, full option +15
// 380 items (no 380 option) and 3rd wings, own buffs only. Every attacker uses the attack skill
// which kills fastest, at the attack interval the server's speed hack detection allows for its
// attack/magic speed. Duels: both start attacking at the same time and drink large complex
// potions whenever the cooldown allows, until one dies.
namespace MUnique.OpenMU.BalanceSim;

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

internal static class Program
{
    private const int MaximumFightSeconds = 600;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static async Task<int> Main(string[] args)
    {
        var options = Options.Parse(args);
        Console.Error.WriteLine($"Loading the game configuration from localhost:{options.Port} ...");
        var config = await LoadConfigurationAsync(options.Port).ConfigureAwait(false);
        var speed = SpeedModel.From(config);

        var classes = config.CharacterClasses.Where(c => c.IsMasterClass).OrderBy(c => c.Number).ToList();
        var context = CreateGameContext(config);

        var fighters = new List<Fighter>();
        foreach (var characterClass in classes)
        {
            Console.Error.WriteLine($"Building {Name(characterClass)} ...");
            fighters.Add(await Fighter.CreateAsync(context, config, characterClass, options).ConfigureAwait(false));
        }

        var results = new Matchup[fighters.Count, fighters.Count];
        for (var a = 0; a < fighters.Count; a++)
        {
            for (var d = 0; d < fighters.Count; d++)
            {
                Console.Error.WriteLine($"{fighters[a].Name} vs {fighters[d].Name} ...");
                results[a, d] = await Matchup.SimulateAsync(fighters[a], fighters[d], speed, options.Samples).ConfigureAwait(false);
            }
        }

        var potion = Potion.LargeComplex(options);
        var duels = new DuelResult[fighters.Count, fighters.Count];
        for (var a = 0; a < fighters.Count; a++)
        {
            for (var d = 0; d < fighters.Count; d++)
            {
                Console.Error.WriteLine($"Duel {fighters[a].Name} vs {fighters[d].Name} ...");
                duels[a, d] = await Duel.RunAsync(fighters[a], fighters[d], results[a, d].Skill, results[d, a].Skill, speed, options.Potions ? potion : null, options).ConfigureAwait(false);
            }
        }

        var report = Report.Create(fighters, results, duels, options, speed, potion);
        Console.WriteLine(report);
        if (options.Output is { } output)
        {
            await File.WriteAllTextAsync(output, report).ConfigureAwait(false);
            Console.Error.WriteLine($"Report written to {Path.GetFullPath(output)}");
        }

        return 0;
    }

    internal static string Name(CharacterClass characterClass) => characterClass.Name.ValueInNeutralLanguage ?? characterClass.Number.ToString(Invariant);

    internal static string Name(Skill skill) => skill.Name.ValueInNeutralLanguage ?? skill.Number.ToString(Invariant);

    internal static string Name(ItemDefinition item) => item.Name.ValueInNeutralLanguage ?? item.Number.ToString(Invariant);

    internal static string Format(double seconds) => double.IsInfinity(seconds) || seconds > MaximumFightSeconds
        ? "∞"
        : seconds < 10 ? seconds.ToString("0.0", Invariant) : seconds.ToString("0", Invariant);

    private static async Task<GameConfiguration> LoadConfigurationAsync(int port)
    {
        // As in Startup: postgres delivers binary data (map terrain) as hex in JSON.
        MUnique.OpenMU.PlugIns.JsonConverterRegistry.RegisterConverter(new MUnique.OpenMU.Persistence.EntityFramework.Json.BinaryAsHexJsonConverter());

        // The file provider loads ConnectionSettings.xml on first use (its InitializeAsync would register itself).
        ConnectionConfigurator.Initialize(new PortOverride(new ConfigFileDatabaseConnectionStringProvider(), port));

        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        var dataSource = new GameConfigurationDataSource(NullLogger<GameConfigurationDataSource>.Instance, provider);
        return (GameConfiguration)await dataSource.GetOwnerAsync().ConfigureAwait(false);
    }

    private static GameContext CreateGameContext(GameConfiguration config)
    {
        var mapInitializer = new MapInitializer(config, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var context = new GameContext(config, new InMemoryPersistenceContextProvider(), mapInitializer, NullLoggerFactory.Instance, new PlugInManager(null, NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = context.PlugInManager;
        mapInitializer.PathFinderPool = context.PathFinderPool;
        return context;
    }

    /// <summary>The connection settings of ConnectionSettings.xml, on another port (the compose stack maps 5433).</summary>
    private sealed class PortOverride : IDatabaseConnectionSettingProvider
    {
        private readonly IDatabaseConnectionSettingProvider _inner;
        private readonly int _port;

        public PortOverride(IDatabaseConnectionSettingProvider inner, int port)
        {
            this._inner = inner;
            this._port = port;
        }

        public Task? Initialization => this._inner.Initialization;

        public Task InitializeAsync(CancellationToken cancellationToken) => this._inner.InitializeAsync(cancellationToken);

        public ConnectionSetting GetConnectionSetting<TContextType>()
            where TContextType : DbContext => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType)
        {
            var setting = this._inner.GetConnectionSetting(contextType);
            return new ConnectionSetting
            {
                ContextTypeName = setting.ContextTypeName,
                DatabaseEngine = setting.DatabaseEngine,
                ConnectionString = setting.ConnectionString?.Replace("Port=5432;", $"Port={this._port};", StringComparison.Ordinal),
            };
        }
    }
}

/// <summary>Command line options.</summary>
internal sealed record Options(int Port, float Stat, int Level, byte ItemLevel, int Samples, bool Gear, bool Buffs, string? Output, int Duels, double DuelSeconds, bool Potions, double? PotionHealth, double? PotionShield, double? PotionCooldown)
{
    public static Options Parse(string[] args)
    {
        string? Value(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
        bool Flag(string name) => args.Contains(name);
        return new Options(
            int.Parse(Value("--port") ?? "5433", CultureInfo.InvariantCulture),
            float.Parse(Value("--stat") ?? "32767", CultureInfo.InvariantCulture),
            int.Parse(Value("--level") ?? "400", CultureInfo.InvariantCulture),
            byte.Parse(Value("--item-level") ?? "15", CultureInfo.InvariantCulture),
            int.Parse(Value("--samples") ?? "4000", CultureInfo.InvariantCulture),
            !Flag("--no-gear"),
            !Flag("--no-buffs"),
            Value("--out"),
            int.Parse(Value("--duels") ?? "10", CultureInfo.InvariantCulture),
            double.Parse(Value("--duel-seconds") ?? "180", CultureInfo.InvariantCulture),
            !Flag("--no-potions"),
            Value("--potion-hp") is { } potionHealth ? double.Parse(potionHealth, CultureInfo.InvariantCulture) : null,
            Value("--potion-sd") is { } potionShield ? double.Parse(potionShield, CultureInfo.InvariantCulture) : null,
            Value("--potion-cooldown") is { } potionCooldown ? double.Parse(potionCooldown, CultureInfo.InvariantCulture) : null);
    }
}

/// <summary>How fast a character attacks: the interval the speed hack detection allows.</summary>
internal sealed record SpeedModel(double BaseDelayMs, double ScalingFactor, double MinimumIntervalMs)
{
    public static SpeedModel From(GameConfiguration config)
    {
        var json = config.PlugInConfigurations.FirstOrDefault(c => c.CustomConfiguration?.Contains("AttackSpeedBaseDelayMs", StringComparison.Ordinal) == true)?.CustomConfiguration;
        if (json is null)
        {
            return new SpeedModel(450, 1.2, 60);
        }

        using var document = JsonDocument.Parse(json);
        double Get(string name, double fallback) => document.RootElement.TryGetProperty(name, out var value) ? value.GetDouble() : fallback;
        return new SpeedModel(Get("AttackSpeedBaseDelayMs", 450), Get("AttackSpeedScalingFactor", 1.2), Get("AttackSpeedMinIntervalMs", 60));
    }

    public double IntervalSeconds(float speed) => Math.Max(this.MinimumIntervalMs, this.BaseDelayMs - (speed * this.ScalingFactor)) / 1000.0;
}

/// <summary>A character of one class, built for the simulation.</summary>
internal sealed class Fighter
{
    // Skills the model leaves out: they need a Fenrir or a shield skill, or they are
    // curses for the enemy which the configuration lists as buffs.
    private static readonly HashSet<string> NotModeled = new(StringComparer.OrdinalIgnoreCase)
    {
        "Plasma Storm", "Defense", "Sleep", "Weakness", "Innovation",
    };

    private Fighter(Player player, CharacterClass characterClass, IReadOnlyList<string> gear, IReadOnlyList<string> buffs, IReadOnlyList<SkillEntry> attackSkills)
    {
        this.Player = player;
        this.Class = characterClass;
        this.Gear = gear;
        this.Buffs = buffs;
        this.AttackSkills = attackSkills;
    }

    public Player Player { get; }

    public CharacterClass Class { get; }

    public string Name => Program.Name(this.Class);

    public IReadOnlyList<string> Gear { get; }

    public IReadOnlyList<string> Buffs { get; }

    public IReadOnlyList<SkillEntry> AttackSkills { get; }

    public IAttributeSystem Attributes => this.Player.Attributes!;

    private Func<Task<Fighter>>? _create;

    /// <summary>Builds a second character of the same class and gear (for a mirror match).</summary>
    public Task<Fighter> CloneAsync() => this._create!();

    public static async Task<Fighter> CreateAsync(IGameContext context, GameConfiguration config, CharacterClass characterClass, Options options)
    {
        var character = new BasicModel.Character
        {
            Name = Program.Name(characterClass),
            CharacterClass = characterClass,
            CurrentMap = config.Maps.FirstOrDefault(m => m.Number == 0),
            Inventory = new BasicModel.ItemStorage(),
        };
        foreach (var stat in characterClass.StatAttributes)
        {
            var value = stat.BaseValue;
            if (stat.Attribute == Stats.Level)
            {
                value = options.Level;
            }
            else if (stat.Attribute == Stats.BaseStrength || stat.Attribute == Stats.BaseAgility || stat.Attribute == Stats.BaseVitality
                     || stat.Attribute == Stats.BaseEnergy || stat.Attribute == Stats.BaseLeadership)
            {
                value = options.Stat;
            }

            character.Attributes.Add(new BasicModel.StatAttribute(stat.Attribute!, value));
        }

        var gear = new List<string>();
        if (options.Gear)
        {
            foreach (var item in Equipment.Select(config, characterClass, options.ItemLevel))
            {
                character.Inventory.Items.Add(item);
                gear.Add($"{Program.Name(item.Definition!)} +{item.Level}{(Equipment.Is380(item.Definition!) ? " (380)" : string.Empty)}");
            }
        }

        var player = new SimPlayer(context) { Account = new BasicModel.Account() };
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await player.SetSelectedCharacterAsync(character).ConfigureAwait(false);

        var skills = config.Skills
            .Where(s => s.MasterDefinition is null && s.QualifiedCharacters.Contains(characterClass) && !NotModeled.Contains(Program.Name(s)))
            .Where(s => Program.Name(s) != "Earthshake" || character.Inventory.Items.Any(i => i.ItemSlot == InventoryConstants.PetSlot)) // needs the Dark Horse
            .ToList();
        var buffs = new List<string>();
        if (options.Buffs)
        {
            foreach (var skill in skills.Where(s => s.SkillType == SkillType.Buff && s.MagicEffectDef is not null))
            {
                var entry = new BasicModel.SkillEntry { Skill = skill, Level = 0 };
                for (var attempt = 0; attempt < 20 && !IsActive(player, skill.MagicEffectDef!); attempt++)
                {
                    await player.ApplyMagicEffectAsync(player, entry).ConfigureAwait(false);
                }

                if (IsActive(player, skill.MagicEffectDef!))
                {
                    buffs.Add(Program.Name(skill));
                }
            }
        }

        var attackSkills = skills
            .Where(s => s.SkillType is SkillType.DirectHit or SkillType.AreaSkillAutomaticHits or SkillType.AreaSkillExplicitHits or SkillType.AreaSkillExplicitTarget)
            .Where(s => s.DamageType != DamageType.None)
            .Select(s => (SkillEntry)new BasicModel.SkillEntry { Skill = s, Level = 0 })
            .ToList();

        var fighter = new Fighter(player, characterClass, gear, buffs, attackSkills);
        fighter._create = () => CreateAsync(context, config, characterClass, options);
        return fighter;
    }

    public void Refill(float shield)
    {
        this.Attributes[Stats.CurrentHealth] = this.Attributes[Stats.MaximumHealth];
        this.Attributes[Stats.CurrentMana] = this.Attributes[Stats.MaximumMana];
        this.Attributes[Stats.CurrentShield] = shield;
    }

    private static bool IsActive(Player player, MagicEffectDefinition effect)
        => player.MagicEffectList.GetActiveEffectsSnapshot().Any(e => e.Definition == effect);

    private sealed class SimPlayer : Player
    {
        public SimPlayer(IGameContext gameContext)
            : base(gameContext)
        {
        }

        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new NullViews();
    }

    private sealed class NullViews : ICustomPlugInContainer<IViewPlugIn>
    {
        private readonly Dictionary<Type, IViewPlugIn> _views = new();

        public T GetPlugIn<T>()
            where T : class, IViewPlugIn
        {
            if (!this._views.TryGetValue(typeof(T), out var view))
            {
                view = new Mock<T>().Object;
                this._views.Add(typeof(T), view);
            }

            return (T)view;
        }
    }
}

/// <summary>
/// The reference gear: 380 items (where the class has them) in every slot, the class's usual weapons
/// - two swords for the Blade Master, two claws for the Fist Master, stick and book for the
/// Dimension Master, staff and shield for the Grand Master, scepter, shield and a max level Dark
/// Horse for the Lord Emperor - and its 3rd wings; all +15 with luck, the highest option and every excellent
/// (and wing) option, but no 380 option.
/// </summary>
internal static class Equipment
{
    private const byte Swords = 0;
    private const byte Scepters = 2;
    private const byte Bows = 4;
    private const byte Staffs = 5;
    private const byte Shields = 6;
    private const byte Pets = 13;
    private const short DarkHorse = 4;
    private const byte Helm = 7;
    private const byte Armor = 8;
    private const byte Pants = 9;
    private const byte Gloves = 10;
    private const byte Boots = 11;
    private const byte Wings = 12;

    // The 3rd wings / capes (group 12).
    private static readonly HashSet<short> ThirdWings = new() { 36, 37, 38, 39, 40, 43, 50 };

    public static IEnumerable<BasicModel.Item> Select(GameConfiguration config, CharacterClass characterClass, byte level)
    {
        bool Wearable(ItemDefinition item) => item.QualifiedCharacters.Contains(characterClass) && item.MaximumSockets == 0 && !item.IsAmmunition;
        bool Fits(ItemDefinition item, byte slot) => item.ItemSlot?.ItemSlots.Contains(slot) == true;
        ItemDefinition? Best(IEnumerable<ItemDefinition> items, Func<ItemDefinition, float> power)
            => items.OrderByDescending(Is380).ThenByDescending(power).ThenByDescending(i => i.DropLevel).FirstOrDefault();

        var slots = new (byte Slot, byte Group)[]
        {
            (InventoryConstants.HelmSlot, Helm),
            (InventoryConstants.ArmorSlot, Armor),
            (InventoryConstants.PantsSlot, Pants),
            (InventoryConstants.GlovesSlot, Gloves),
            (InventoryConstants.BootsSlot, Boots),
        };
        foreach (var (slot, group) in slots)
        {
            if (Best(config.Items.Where(i => i.Group == group && Wearable(i)), i => i.DropLevel) is { } armor)
            {
                yield return Create(armor, slot, level);
            }
        }

        var weapons = config.Items.Where(i => i.Group <= Staffs && Wearable(i) && Fits(i, InventoryConstants.LeftHandSlot)).ToList();
        float Physical(ItemDefinition i) => Power(i, Stats.MaximumPhysBaseDmgByWeapon);
        float Wizardry(ItemDefinition i) => Power(i, Stats.StaffRise);
        var className = Program.Name(characterClass);
        switch (className)
        {
            case "Blade Master":
                if (Best(weapons.Where(i => i.Group == Swords && Fits(i, InventoryConstants.RightHandSlot)), Physical) is { } sword)
                {
                    yield return Create(sword, InventoryConstants.LeftHandSlot, level);
                    yield return Create(sword, InventoryConstants.RightHandSlot, level);
                }

                break;
            case "Fist Master":
                if (Best(weapons.Where(i => Fits(i, InventoryConstants.RightHandSlot)), Physical) is { } claw)
                {
                    yield return Create(claw, InventoryConstants.LeftHandSlot, level);
                    yield return Create(claw, InventoryConstants.RightHandSlot, level);
                }

                break;
            case "Dimension Master":
                if (Best(weapons.Where(i => i.Group == Staffs), Wizardry) is { } stick)
                {
                    yield return Create(stick, InventoryConstants.LeftHandSlot, level);
                }

                var books = config.Items.Where(i => Wearable(i) && Power(i, Stats.BookRise) > 0 && Fits(i, InventoryConstants.RightHandSlot));
                if (Best(books, i => Power(i, Stats.BookRise)) is { } book)
                {
                    yield return Create(book, InventoryConstants.RightHandSlot, level);
                }

                break;
            default:
                var (group, power) = className switch
                {
                    "Grand Master" => (Staffs, (Func<ItemDefinition, float>)Wizardry),
                    "High Elf" => (Bows, Physical),
                    "Lord Emperor" => (Scepters, i => Power(i, Stats.ScepterRise) + Physical(i)),
                    _ => (Swords, Physical),
                };
                if (Best(weapons.Where(i => i.Group == group), power) is { } weapon)
                {
                    yield return Create(weapon, InventoryConstants.LeftHandSlot, level);
                }

                // Grand Master: staff and shield; Lord Emperor: scepter and shield, riding a max level Dark Horse.
                if (className is "Grand Master" or "Lord Emperor"
                    && Best(config.Items.Where(i => i.Group == Shields && Wearable(i) && Fits(i, InventoryConstants.RightHandSlot)), i => Power(i, Stats.DefenseBase)) is { } shield)
                {
                    yield return Create(shield, InventoryConstants.RightHandSlot, level);
                }

                if (className == "Lord Emperor"
                    && config.Items.FirstOrDefault(i => i.Group == Pets && i.Number == DarkHorse && Wearable(i)) is { } horse)
                {
                    yield return Create(horse, InventoryConstants.PetSlot, horse.MaximumItemLevel);
                }

                break;
        }

        var wings = config.Items.Where(i => i.Group == Wings && ThirdWings.Contains(i.Number) && Wearable(i) && Fits(i, InventoryConstants.WingsSlot));
        if (wings.FirstOrDefault() is { } wing)
        {
            yield return Create(wing, InventoryConstants.WingsSlot, level);
        }
    }

    public static bool Is380(ItemDefinition item)
        => item.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.GuardianOption));

    private static float Power(ItemDefinition item, AttributeDefinition attribute)
        => item.BasePowerUpAttributes.FirstOrDefault(a => a.TargetAttribute == attribute)?.BaseValue ?? 0;

    private static BasicModel.Item Create(ItemDefinition definition, byte slot, byte level)
    {
        var item = new BasicModel.Item
        {
            Id = Guid.NewGuid(), // items are compared by id
            Definition = definition,
            ItemSlot = slot,
            Level = Math.Min(level, definition.MaximumItemLevel),
            Durability = definition.Durability,
            HasSkill = definition.Skill is not null,
        };

        // Luck, the highest additional option, every excellent and wing option - no 380 option.
        foreach (var option in definition.PossibleItemOptions.SelectMany(o => o.PossibleOptions))
        {
            if (option.OptionType == ItemOptionTypes.Luck || option.OptionType == ItemOptionTypes.Excellent
                || option.OptionType == ItemOptionTypes.Wing || option.OptionType == ItemOptionTypes.DarkHorse)
            {
                item.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = option, Level = 0 });
            }
            else if (option.OptionType == ItemOptionTypes.Option)
            {
                var maximum = option.LevelDependentOptions.Select(l => l.Level).DefaultIfEmpty(1).Max();
                item.ItemOptions.Add(new BasicModel.ItemOptionLink { ItemOption = option, Level = maximum });
            }
        }

        return item;
    }
}

/// <summary>One attacker against one defender.</summary>
internal sealed record Matchup(string Skill, double HitChance, double DamagePerHit, double IntervalSeconds, double KillSeconds, double CastsToKill)
{
    public static async Task<Matchup> SimulateAsync(Fighter attacker, Fighter defender, SpeedModel speed, int samples)
    {
        Matchup? best = null;
        foreach (var skill in attacker.AttackSkills)
        {
            var quick = await SimulateSkillAsync(attacker, defender, skill, speed, Math.Max(200, samples / 10)).ConfigureAwait(false);
            if (best is null || quick.KillSeconds < best.KillSeconds)
            {
                best = quick;
            }
        }

        if (best is null)
        {
            return new Matchup("(no attack skill)", 0, 0, 0, double.PositiveInfinity, double.PositiveInfinity);
        }

        var chosen = attacker.AttackSkills.First(s => Program.Name(s.Skill!) == best.Skill);
        return await SimulateSkillAsync(attacker, defender, chosen, speed, samples).ConfigureAwait(false);
    }

    // Two phases: while the defender has shield (SD), most damage goes to it; after that all to health.
    private static async Task<Matchup> SimulateSkillAsync(Fighter attacker, Fighter defender, SkillEntry skill, SpeedModel speed, int samples)
    {
        var maxShield = defender.Attributes[Stats.MaximumShield];
        var maxHealth = defender.Attributes[Stats.MaximumHealth];

        defender.Refill(maxShield);
        var (shieldPhaseHealth, shieldPhaseShield, hits) = await SampleAsync(attacker, defender, skill, samples).ConfigureAwait(false);
        defender.Refill(0);
        var (healthPhaseHealth, _, _) = await SampleAsync(attacker, defender, skill, samples).ConfigureAwait(false);
        defender.Refill(maxShield);

        double castsToKill;
        if (maxShield >= 1 && shieldPhaseShield > 0)
        {
            var castsToBreakShield = maxShield / shieldPhaseShield;
            var healthLeft = maxHealth - (castsToBreakShield * shieldPhaseHealth);
            castsToKill = healthLeft <= 0
                ? maxHealth / Math.Max(shieldPhaseHealth, double.Epsilon)
                : castsToBreakShield + (healthLeft / Math.Max(healthPhaseHealth, double.Epsilon));
        }
        else
        {
            castsToKill = maxHealth / Math.Max(healthPhaseHealth, double.Epsilon);
        }

        var attackSpeed = skill.Skill!.DamageType == DamageType.Physical ? attacker.Attributes[Stats.AttackSpeed] : attacker.Attributes[Stats.MagicSpeed];
        var interval = speed.IntervalSeconds(attackSpeed);
        return new Matchup(
            Program.Name(skill.Skill!),
            hits / (double)samples,
            healthPhaseHealth,
            interval,
            castsToKill * interval,
            castsToKill);
    }

    // Average health and shield damage of one cast (all of its hits), and how many casts hit.
    private static async Task<(double Health, double Shield, int Hits)> SampleAsync(Fighter attacker, Fighter defender, SkillEntry skill, int samples)
    {
        var hitsPerCast = HitsPerCast(skill.Skill!);
        double health = 0;
        double shield = 0;
        var hits = 0;
        var startShield = defender.Attributes[Stats.CurrentShield];
        for (var i = 0; i < samples; i++)
        {
            var anyHit = false;
            for (var h = 0; h < hitsPerCast; h++)
            {
                var hit = await attacker.Player.CalculateDamageAsync(defender.Player, skill, false).ConfigureAwait(false);
                health += hit.HealthDamage;
                shield += hit.ShieldDamage;
                anyHit |= hit.TotalDamage > 0;
                defender.Attributes[Stats.CurrentShield] = startShield;
            }

            hits += anyHit ? 1 : 0;
        }

        return (health / samples, shield / samples, hits);
    }

    public static int HitsPerCast(Skill skill)
    {
        if (skill.AreaSkillSettings is { MaximumNumberOfHitsPerTarget: > 1 } settings)
        {
            return settings.MaximumNumberOfHitsPerTarget;
        }

        return Math.Max(1, (int)skill.NumberOfHitsPerAttack);
    }
}

/// <summary>
/// A large complex potion as the server applies it: the large healing and the large shield potion
/// handlers with their default configuration (the complex potion handler creates its own instances,
/// so the admin panel's potion settings don't apply to it), each recovering in steps, and a cooldown.
/// </summary>
internal sealed record Potion(RecoverConsumeHandlerConfiguration Health, RecoverConsumeHandlerConfiguration Shield)
{
    public TimeSpan Cooldown => this.Health.CooldownTime > this.Shield.CooldownTime ? this.Health.CooldownTime : this.Shield.CooldownTime;

    public static Potion LargeComplex(Options options)
    {
        var configuration = (
            Health: (RecoverConsumeHandlerConfiguration)new LargeHealthPotionConsumeHandlerPlugIn().CreateDefaultConfig(),
            Shield: (RecoverConsumeHandlerConfiguration)new LargeShieldPotionConsumeHandlerPlugIn().CreateDefaultConfig());
        if (options.PotionHealth is { } health)
        {
            configuration.Health.TotalRecoverPercentage = health;
        }

        if (options.PotionShield is { } shield)
        {
            configuration.Shield.TotalRecoverPercentage = shield;
        }

        if (options.PotionCooldown is { } cooldown)
        {
            configuration.Health.CooldownTime = TimeSpan.FromSeconds(cooldown);
            configuration.Shield.CooldownTime = TimeSpan.FromSeconds(cooldown);
        }

        return new Potion(configuration.Health, configuration.Shield);
    }

    // (seconds after drinking, health, shield) - the recovery of one potion, as RecoverConsumeHandlerPlugIn does it.
    public IEnumerable<(double At, double Health, double Shield)> Recovery(IAttributeSystem attributes)
    {
        foreach (var (configuration, maximum, isHealth) in new[] { (this.Health, Stats.MaximumHealth, true), (this.Shield, Stats.MaximumShield, false) })
        {
            var total = (attributes[maximum] * configuration.TotalRecoverPercentage / 100.0)
                        + Math.Max(0, configuration.AdditionalRecoverMinusCharacterLevel - attributes[Stats.Level]);
            if (configuration.RecoverSteps.Count == 0)
            {
                yield return (0, isHealth ? total : 0, isHealth ? 0 : total);
                continue;
            }

            var at = 0.0;
            foreach (var step in configuration.RecoverSteps)
            {
                at += step.Delay.TotalSeconds;
                var amount = total * step.RecoverPercentage / 100.0;
                yield return (at, isHealth ? amount : 0, isHealth ? 0 : amount);
            }
        }
    }

    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"{this.Health.TotalRecoverPercentage:0}% HP and {this.Shield.TotalRecoverPercentage:0}% SD over {this.Health.RecoverSteps.Sum(s => s.Delay.TotalSeconds):0.#} s, one every {this.Cooldown.TotalSeconds:0.##} s");
}

/// <summary>The outcome of the duels of one class (row) against another (column).</summary>
internal sealed record DuelResult(double Wins, double Losses, double Draws, double MedianSeconds, double PotionsPerMinute);

/// <summary>
/// Duels: both start attacking at (almost) the same time, each with its best skill against the other
/// at its own attack interval, and drink a potion whenever the cooldown allows and they are hurt.
/// </summary>
internal static class Duel
{
    private const double Tick = 0.01;

    public static async Task<DuelResult> RunAsync(Fighter first, Fighter second, string firstSkill, string secondSkill, SpeedModel speed, Potion? potion, Options options)
    {
        var a = first;
        var b = second;
        if (ReferenceEquals(first, second))
        {
            // A mirror match needs two characters; the second one is the same build.
            b = await first.CloneAsync().ConfigureAwait(false);
        }

        int wins = 0, losses = 0, draws = 0;
        var durations = new List<double>();
        double potions = 0;
        for (var i = 0; i < options.Duels; i++)
        {
            var sideA = new Side(a, a.AttackSkills.First(s => Program.Name(s.Skill!) == firstSkill), speed);
            var sideB = new Side(b, b.AttackSkills.First(s => Program.Name(s.Skill!) == secondSkill), speed);
            var t = 0.0;
            for (; t < options.DuelSeconds && sideA.Health > 0 && sideB.Health > 0; t += Tick)
            {
                sideA.Recover(t, potion);
                sideB.Recover(t, potion);
                if (t >= sideA.NextAttack)
                {
                    await sideA.AttackAsync(sideB).ConfigureAwait(false);
                }

                if (t >= sideB.NextAttack)
                {
                    await sideB.AttackAsync(sideA).ConfigureAwait(false);
                }
            }

            if (sideA.Health > 0 && sideB.Health <= 0)
            {
                wins++;
            }
            else if (sideB.Health > 0 && sideA.Health <= 0)
            {
                losses++;
            }
            else
            {
                draws++;
            }

            durations.Add(t);
            potions += sideA.Potions / Math.Max(t / 60, 1.0 / 60);
        }

        a.Refill(a.Attributes[Stats.MaximumShield]);
        b.Refill(b.Attributes[Stats.MaximumShield]);
        durations.Sort();
        return new DuelResult(wins / (double)options.Duels, losses / (double)options.Duels, draws / (double)options.Duels, durations[durations.Count / 2], potions / options.Duels);
    }

    private sealed class Side
    {
        private readonly Fighter _fighter;
        private readonly SkillEntry _skill;
        private readonly double _interval;
        private readonly int _hitsPerCast;
        private readonly List<(double At, double Health, double Shield)> _recovering = new();
        private double _potionReady;

        public Side(Fighter fighter, SkillEntry skill, SpeedModel speed)
        {
            this._fighter = fighter;
            this._skill = skill;
            var attributes = fighter.Attributes;
            var attackSpeed = skill.Skill!.DamageType == DamageType.Physical ? attributes[Stats.AttackSpeed] : attributes[Stats.MagicSpeed];
            this._interval = speed.IntervalSeconds(attackSpeed);
            this._hitsPerCast = Matchup.HitsPerCast(skill.Skill);
            this.Health = attributes[Stats.MaximumHealth];
            this.Shield = attributes[Stats.MaximumShield];
            this.NextAttack = Random.Shared.NextDouble() * 0.2; // "at around the same time"
        }

        public double Health { get; private set; }

        public double Shield { get; private set; }

        public double NextAttack { get; private set; }

        public int Potions { get; private set; }

        private IAttributeSystem Attributes => this._fighter.Attributes;

        public void Recover(double now, Potion? potion)
        {
            for (var i = this._recovering.Count - 1; i >= 0; i--)
            {
                var (at, health, shield) = this._recovering[i];
                if (at <= now)
                {
                    this.Health = Math.Min(this.Attributes[Stats.MaximumHealth], this.Health + health);
                    this.Shield = Math.Min(this.Attributes[Stats.MaximumShield], this.Shield + shield);
                    this._recovering.RemoveAt(i);
                }
            }

            var hurt = this.Health < this.Attributes[Stats.MaximumHealth] || this.Shield < this.Attributes[Stats.MaximumShield];
            if (potion is not null && hurt && now >= this._potionReady)
            {
                this._recovering.AddRange(potion.Recovery(this.Attributes).Select(r => (now + r.At, r.Health, r.Shield)));
                this._potionReady = now + potion.Cooldown.TotalSeconds;
                this.Potions++;
            }
        }

        public async ValueTask AttackAsync(Side target)
        {
            this.NextAttack += this._interval;
            for (var h = 0; h < this._hitsPerCast && target.Health > 0; h++)
            {
                // The server reads the defender's current shield and mana (Soul Barrier) for the hit.
                target._fighter.Refill((float)target.Shield);
                var hit = await this._fighter.Player.CalculateDamageAsync(target._fighter.Player, this._skill, false).ConfigureAwait(false);
                target.Health -= hit.HealthDamage;
                target.Shield = Math.Max(0, target.Shield - hit.ShieldDamage);
            }
        }
    }
}

/// <summary>The markdown report.</summary>
internal static class Report
{
    public static string Create(IReadOnlyList<Fighter> fighters, Matchup[,] results, DuelResult[,] duels, Options options, SpeedModel speed, Potion potion)
    {
        var invariant = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine("# PvP balance report");
        text.AppendLine();
        text.AppendLine(string.Create(invariant, $"All 3rd class characters at level {options.Level}, every base stat {options.Stat:N0}, no master skills, ")
            + (options.Gear ? string.Create(invariant, $"+{options.ItemLevel} full option 380 items (no 380 option) and 3rd wings") : "no gear")
            + (options.Buffs ? ", own buffs active." : ", no buffs.")
            );
        text.AppendLine();

        text.AppendLine("## Duels");
        text.AppendLine();
        text.AppendLine(string.Create(invariant, $"{options.Duels} duels per pair, both attacking from the start, up to {options.DuelSeconds:0} s each. ")
            + (options.Potions ? $"Both drink large complex potions ({potion.Describe()}) whenever they are hurt." : "No potions."));
        text.AppendLine();
        text.AppendLine("### Wins of the row against the column");
        text.AppendLine();
        text.AppendLine("Draws (nobody died in time) make up the rest.");
        text.AppendLine();
        AppendMatrix(text, fighters, (a, d) => duels[a, d].Wins.ToString("P0", invariant) + (duels[a, d].Draws > 0 ? $" ({duels[a, d].Draws.ToString("P0", invariant)} draw)" : string.Empty));
        text.AppendLine();
        text.AppendLine("### Duel length (median seconds)");
        text.AppendLine();
        AppendMatrix(text, fighters, (a, d) => duels[a, d].MedianSeconds >= options.DuelSeconds ? "draw" : duels[a, d].MedianSeconds.ToString("0.0", invariant));
        text.AppendLine();
        text.AppendLine("### Overall");
        text.AppendLine();
        text.AppendLine("| class | wins | losses | draws | potions / minute |");
        text.AppendLine("|---|---:|---:|---:|---:|");
        for (var a = 0; a < fighters.Count; a++)
        {
            var others = Enumerable.Range(0, fighters.Count).Where(d => d != a).Select(d => duels[a, d]).ToList();
            text.AppendLine(invariant, $"| {fighters[a].Name} | {others.Average(r => r.Wins):P0} | {others.Average(r => r.Losses):P0} | {others.Average(r => r.Draws):P0} | {others.Average(r => r.PotionsPerMinute):0} |");
        }

        text.AppendLine();
        text.AppendLine("### How close each matchup is");
        text.AppendLine();
        text.AppendLine("Duel outcomes are nearly deterministic: the side with any edge wins almost every time. This shows the size of the edge - how much faster the stronger side kills (from the casts to kill below; 1.00 = even).");
        text.AppendLine();
        text.AppendLine("| edge | stronger | weaker |");
        text.AppendLine("|---:|---|---|");
        var pairs = new List<(double Edge, string Strong, string Weak)>();
        for (var a = 0; a < fighters.Count; a++)
        {
            for (var b = a + 1; b < fighters.Count; b++)
            {
                var ab = results[a, b].CastsToKill;
                var ba = results[b, a].CastsToKill;
                pairs.Add(ab <= ba ? (ba / ab, fighters[a].Name, fighters[b].Name) : (ab / ba, fighters[b].Name, fighters[a].Name));
            }
        }

        foreach (var (edge, strong, weak) in pairs.OrderByDescending(p => p.Edge))
        {
            text.AppendLine(invariant, $"| {edge:0.00} | {strong} | {weak} |");
        }

        text.AppendLine();
        text.AppendLine("## Without potions");
        text.AppendLine();
        text.AppendLine("### Time to kill (seconds)");
        text.AppendLine();
        text.AppendLine("Row attacks column, the column only takes the hits. Lower = stronger attacker. ∞ = more than 10 minutes.");
        text.AppendLine();
        text.Append("| attacker \\ defender |");
        foreach (var f in fighters)
        {
            text.Append(invariant, $" {f.Name} |");
        }

        text.AppendLine(" median |");
        text.Append("|---|");
        text.AppendLine(string.Concat(Enumerable.Repeat("---:|", fighters.Count + 1)));
        for (var a = 0; a < fighters.Count; a++)
        {
            text.Append(invariant, $"| **{fighters[a].Name}** |");
            for (var d = 0; d < fighters.Count; d++)
            {
                text.Append(invariant, $" {Program.Format(results[a, d].KillSeconds)} |");
            }

            text.AppendLine(invariant, $" **{Program.Format(Median(Enumerable.Range(0, fighters.Count).Select(d => results[a, d].KillSeconds)))}** |");
        }

        text.Append("| **median survived** |");
        for (var d = 0; d < fighters.Count; d++)
        {
            text.Append(invariant, $" **{Program.Format(Median(Enumerable.Range(0, fighters.Count).Select(a => results[a, d].KillSeconds)))}** |");
        }

        text.AppendLine(" |");
        text.AppendLine();

        text.AppendLine("### Casts to kill");
        text.AppendLine();
        text.AppendLine("The same fights counted in attacks instead of seconds - independent of attack speed.");
        text.AppendLine();
        AppendMatrix(text, fighters, (a, d) => results[a, d].CastsToKill is var v && (double.IsInfinity(v) || v > 100_000) ? "∞" : v.ToString(v < 10 ? "0.0" : "N0", CultureInfo.InvariantCulture));
        text.AppendLine();

        text.AppendLine("## Classes");
        text.AppendLine();
        text.AppendLine("| class | HP | SD | defense (PvP) | defense rate (PvP) | attack rate (PvP) | attack / magic speed | damage taken | buffs |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var f in fighters)
        {
            var a = f.Attributes;
            text.AppendLine(invariant, $"| {f.Name} | {a[Stats.MaximumHealth]:N0} | {a[Stats.MaximumShield]:N0} | {a[Stats.DefensePvp]:N0} | {a[Stats.DefenseRatePvp]:N0} | {a[Stats.AttackRatePvp]:N0} | {a[Stats.AttackSpeed]:N0} / {a[Stats.MagicSpeed]:N0} | {a[Stats.DamageReceiveDecrement]:P0} | {string.Join(", ", f.Buffs)} |");
        }

        text.AppendLine();
        text.AppendLine("## Attacks used");
        text.AppendLine();
        text.AppendLine("Best skill per matchup, hit chance, health damage per cast once the shield is gone, and casts per second.");
        text.AppendLine();
        text.AppendLine("| attacker | defender | skill | hit chance | damage / cast | casts / s |");
        text.AppendLine("|---|---|---|---:|---:|---:|");
        for (var a = 0; a < fighters.Count; a++)
        {
            for (var d = 0; d < fighters.Count; d++)
            {
                if (a == d)
                {
                    continue;
                }

                var r = results[a, d];
                text.AppendLine(invariant, $"| {fighters[a].Name} | {fighters[d].Name} | {r.Skill} | {r.HitChance:P0} | {r.DamagePerHit:N0} | {(r.IntervalSeconds > 0 ? 1 / r.IntervalSeconds : 0):0.0} |");
            }
        }

        text.AppendLine();
        text.AppendLine("## Gear");
        text.AppendLine();
        foreach (var f in fighters)
        {
            text.AppendLine(invariant, $"- **{f.Name}**: {(f.Gear.Count > 0 ? string.Join(", ", f.Gear) : "none")}");
        }

        text.AppendLine();
        text.AppendLine("## Notes");
        text.AppendLine();
        text.AppendLine(invariant, $"- Attacks per second follow the speed hack detection: an attack every max({speed.MinimumIntervalMs} ms, {speed.BaseDelayMs} ms − {speed.ScalingFactor} × speed).");
        text.AppendLine("- Not simulated: ancient / socket / harmony / 380 options, master skill tree, Fenrir, debuffs on the enemy (e.g. Summoner curses), reflect, regeneration, movement and range. Mana and AG never run out (as if mana potions keep up).");
        text.AppendLine("- Damage is the server's own calculation (AttackableExtensions.CalculateDamageAsync), including misses, criticals, excellent and double damage, defense, SD and Soul Barrier.");
        return text.ToString();
    }

    private static void AppendMatrix(StringBuilder text, IReadOnlyList<Fighter> fighters, Func<int, int, string> cell)
    {
        text.Append(@"| attacker \ defender |");
        foreach (var f in fighters)
        {
            text.Append(CultureInfo.InvariantCulture, $" {f.Name} |");
        }

        text.AppendLine();
        text.Append("|---|");
        text.AppendLine(string.Concat(Enumerable.Repeat("---:|", fighters.Count)));
        for (var a = 0; a < fighters.Count; a++)
        {
            text.Append(CultureInfo.InvariantCulture, $"| **{fighters[a].Name}** |");
            for (var d = 0; d < fighters.Count; d++)
            {
                text.Append(CultureInfo.InvariantCulture, $" {cell(a, d)} |");
            }

            text.AppendLine();
        }
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }
}
