using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Gw2Giveaway.Services;

namespace Gw2Giveaway.Services;

public record QuestionData(
    string Category,
    string QuestionText,
    string[] Options,
    int CorrectIndex,
    string CorrectLetter,
    string CorrectFull
);

public class TriviaService
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.guildwars2.com/v2/") };
    private List<int>? _itemIds;
    private List<int>? _skillIds;
    private List<int>? _specIds;
    private List<string>? _professions;
    private List<Gw2Color>? _colors;
    private readonly Random _rand = new();

    private readonly string[] _professionOptions = { "Guardian", "Warrior", "Engineer", "Ranger", "Thief", "Elementalist", "Mesmer", "Necromancer", "Revenant" };


    private readonly List<(string Question, string[] Options, string Correct)> _loreQuestions = new()
{
    ("Which Elder Dragon is associated with plant and mind corruption?", new[] { "Zhaitan", "Mordremoth", "Kralkatorrik", "Jormag" }, "Mordremoth"),
    ("What is the name of the human god of war, fire, and challenge?", new[] { "Balthazar", "Grenth", "Dwayna", "Lyssa" }, "Balthazar"),
    ("Which race was awakened from seeds by the Pale Tree?", new[] { "Asura", "Charr", "Sylvari", "Norn" }, "Sylvari"),
    ("What is the capital city of the Norn?", new[] { "Rata Sum", "Hoelbrak", "Black Citadel", "Divinity's Reach" }, "Hoelbrak"),
    ("Which Elder Dragon consumes magic itself?", new[] { "Primordus", "Soo-Won", "Kralkatorrik", "Mordremoth" }, "Soo-Won"),
    ("What is the name of Rytlock Brimstone's legendary sword?", new[] { "Caladbolg", "Sohothin", "Kudzu", "Twilight" }, "Sohothin"),
    ("Which god replaced Abaddon?", new[] { "Kormir", "Balthazar", "Melandru", "Dwayna" }, "Kormir"),
    ("What is the sylvari's nightmare called?", new[] { "Dragon Corruption", "The Dream", "Nightmare Court", "Mordrem Guard" }, "Nightmare Court"),
    ("Who is the current ruler of Kryta?", new[] { "Queen Jennah", "King Adelbern", "Logan Thackeray", "Countess Anise" }, "Queen Jennah"),
    ("Which Elder Dragon is tied to ice and persuasion?", new[] { "Jormag", "Zhaitan", "Primordus", "Kralkatorrik" }, "Jormag"),
    ("Which race built the gates of Arah?", new[] { "Forgotten", "Mursaat", "Seers", "Dwarves" }, "Forgotten"),
    ("Who is Destiny's Edge leader?", new[] { "Eir Stegalkin", "Rytlock Brimstone", "Logan Thackeray", "Zojja" }, "Eir Stegalkin"),
    ("What is the asura's floating city called?", new[] { "Rata Sum", "Rata Novus", "Tarin", "Aerial" }, "Rata Sum"),
    ("Which commander founded Dragon's Watch?", new[] { "The Pact Commander", "Trahearne", "Taimi", "Rox" }, "The Pact Commander"),
    ("Which expansion introduced mounts?", new[] { "Heart of Thorns", "Path of Fire", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("What corrupts sylvari into Mordrem?", new[] { "Shadow of the Dragon", "Mordremoth's Call", "Blight", "Risen" }, "Mordremoth's Call"),
    ("Which race has legions?", new[] { "Charr", "Norn", "Asura", "Human" }, "Charr"),
    ("Which god abandoned Tyria in recent events?", new[] { "Abaddon", "Balthazar", "Kormir", "Melandru" }, "Balthazar"),
    ("What is the name of the sylvari cycle?", new[] { "Dream and Nightmare", "Day and Night", "Light and Shadow", "Life and Death" }, "Dream and Nightmare"),
    ("Which expansion introduced Jade Bots?", new[] { "Path of Fire", "End of Dragons", "Secrets of the Obscure", "Visions of Eternity" }, "End of Dragons"),
    ("What is the name of the charr rebellion against humans?", new[] { "The Searing", "The Foefire", "The Rebellion", "The Olmakhan Uprising" }, "The Searing"),
    ("Which profession was added in Heart of Thorns?", new[] { "Revenant", "Chronomancer", "Scrapper", "All elites" }, "Revenant"),
    ("What is the name of the underwater city in End of Dragons?", new[] { "New Kaineng", "Cantha", "Arborstone", "Jade Sea" }, "New Kaineng"),
    ("Which dragon's death caused the rise of liches?", new[] { "Zhaitan", "Palawa Joko", "Kralkatorrik", "Jormag" }, "Zhaitan"),
    ("What is the name of the asura gate network?", new[] { "Asura Gates", "Waypoints", "Portals", "Gates of Madness" }, "Asura Gates"),
    ("Who is the leader of the Inquest?", new[] { "No single leader", "Zojja", "Snaff", "Kudu" }, "No single leader"),
    ("Which expansion introduced gliding?", new[] { "Heart of Thorns", "Path of Fire", "End of Dragons", "Living World Season 3" }, "Heart of Thorns"),
    ("What is the name of the norn spirit of the wolf?", new[] { "Bear", "Raven", "Snow Leopard", "Wolf" }, "Wolf"),
    ("Which race uses golems as labor?", new[] { "Asura", "Charr", "Sylvari", "Human" }, "Asura"),
    ("What is the name of the charr high legions' capital?", new[] { "Black Citadel", "Hoelbrak", "Rata Sum", "The Grove" }, "Black Citadel"),
    ("Which Elder Dragon was killed in Path of Fire?", new[] { "Kralkatorrik", "Soo-Won", "Mordremoth", "Jormag" }, "Kralkatorrik"),
    ("What is the name of the sylvari first born who turned to nightmare?", new[] { "Faolain", "Caithe", "Trahearne", "Riannoc" }, "Faolain"),
    ("Which human god is associated with healing,air and life?", new[] { "Dwayna", "Grenth", "Lyssa", "Melandru" }, "Dwayna"),
    ("What is the name of the asura college focused on dynamics?", new[] { "College of Dynamics", "College of Synergetics", "College of Statics", "College of Arcane" }, "College of Dynamics"),
    ("Which ancient race sealed the Elder Dragons?", new[] { "Forgotten", "Mursaat", "Seers", "Jotun" }, "Forgotten"),
    ("What is the name of the charr warband of Rytlock Brimstone?", new[] { "Stone Warband", "Blood Warband", "Iron Warband", "Ash Warband" }, "Stone Warband"),
    ("Which expansion introduced skiffs and fishing?", new[] { "End of Dragons", "Path of Fire", "Secrets of the Obscure", "Visions of Eternity" }, "End of Dragons"),
    ("What is the name of the human god of death and ice?", new[] { "Grenth", "Balthazar", "Dwayna", "Lyssa" }, "Grenth"),
    ("Which Elder Dragon is associated with crystal and branded corruption?", new[] { "Kralkatorrik", "Jormag", "Primordus", "Zhaitan" }, "Kralkatorrik"),
    ("What is the name of the norn lodge associated with cunning?", new[] { "Raven Lodge", "Bear Lodge", "Wolf Lodge", "Snow Leopard Lodge" }, "Raven Lodge"),
    ("Which ancient race used unseen magic?", new[] { "Mursaat", "Forgotten", "Seers", "Dwarves" }, "Mursaat"),
    ("What is the name of the sylvari second born who is a ranger?", new[] { "Canach", "Caithe", "Trahearne", "Sayeh" }, "Canach"),
    ("Which expansion introduced the turtle mount?", new[] { "End of Dragons", "Path of Fire", "Secrets of the Obscure", "Visions of Eternity" }, "End of Dragons"),
    ("What is the name of the charr imperator who rebelled in Icebrood Saga?", new[] { "Bangar Ruinbringer", "Smodur the Unflinching", "Malice Swordshadow", "Crecia Stoneglow" }, "Bangar Ruinbringer"),
    ("Which god is associated with illusion and chaos?", new[] { "Lyssa", "Dwayna", "Melandru", "Grenth" }, "Lyssa"),
    ("What is the name of the asura who invented portal technology?", new[] { "Oola", "Zojja", "Snaff", "Taimi" }, "Oola"),
    ("Which Elder Dragon is associated with fire and destruction?", new[] { "Primordus", "Jormag", "Zhaitan", "Soo-Won" }, "Primordus"),
    ("What is the name of the norn spirit of wisdom and cunning?", new[] { "Raven", "Bear", "Wolf", "Snow Leopard" }, "Raven"),
    ("Which race has the Pale Tree as their mother?", new[] { "Sylvari", "Asura", "Norn", "Human" }, "Sylvari"),
    ("What is the name of the human god of nature and earth?", new[] { "Melandru", "Dwayna", "Lyssa", "Balthazar" }, "Melandru"),
    ("Which expansion introduced the raptor as the first mount?", new[] { "Path of Fire", "Heart of Thorns", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("What is the name of the charr legion focused on technology?", new[] { "Iron Legion", "Blood Legion", "Ash Legion", "Flame Legion" }, "Iron Legion"),
    ("Which race has the Vigil as one of their orders?", new[] { "All races", "Human", "Charr", "Sylvari" }, "All races"),
    ("What is the name of the sylvari who purified the Pale Tree?", new[] { "Trahearne", "Caithe", "Canach", "The Pact Commander" }, "Trahearne"),
    ("Which Elder Dragon was killed in End of Dragons?", new[] { "Soo-Won", "Jormag", "Kralkatorrik", "Primordus" }, "Soo-Won"),
    ("What is the name of the asura college focused on stability?", new[] { "College of Statics", "College of Dynamics", "College of Synergetics", "College of Eternity" }, "College of Statics"),
    ("Which race has the Durmand Priory as one of their orders?", new[] { "All races", "Human", "Asura", "Norn" }, "All races"),
    ("What is the name of the human civil war event?", new[] { "Krytan Civil War", "Centaur War", "Human-Charr Conflict", "Orr War" }, "Krytan Civil War"),
    ("What is the name of the norn great lodge?", new[] { "Hoelbrak", "Eye of the North", "Great Lodge", "Homestead" }, "Hoelbrak"),
    ("Which god is associated with knowledge and truth?", new[] { "Kormir", "Dwayna", "Lyssa", "Grenth" }, "Kormir"),
    ("What is the name of the charr imperator of Ash Legion?", new[] { "Malice Swordshadow", "Crecia Stoneglow", "Smodur the Unflinching", "Bangar Ruinbringer" }, "Malice Swordshadow"),
    ("Which race experiences the Dream and Nightmare?", new[] { "Sylvari", "Asura", "Norn", "Charr" }, "Sylvari"),
    ("What is the name of the asura who sacrificed himself against Kralkatorrik?", new[] { "Snaff", "Zojja", "Taimi", "Oola" }, "Snaff"),
    ("What is the name of the norn spirit of strength?", new[] { "Bear", "Wolf", "Raven", "Snow Leopard" }, "Bear"),
    ("Which race has the Olmakhan as a pacifist tribe?", new[] { "Charr", "Human", "Sylvari", "Norn" }, "Charr"),
    ("What is the name of the human god of light and truth?", new[] { "Kormir", "Dwayna", "Lyssa", "Melandru" }, "Kormir"),
    ("Which expansion introduced the skyscale mount?", new[] { "Living World Season 4", "Path of Fire", "End of Dragons", "Secrets of the Obscure" }, "Living World Season 4"),
    ("What is the name of the charr who led the Flame Legion in the past?", new[] { "Gaheron Baelfire", "Bangar Ruinbringer", "Smodur", "Malice" }, "Gaheron Baelfire"),
    ("Which race has the Pale Tree as their birthplace?", new[] { "Sylvari", "Asura", "Norn", "Human" }, "Sylvari"),
    ("What is the name of the asura college focused on temporal eternity?", new[] { "College of Synergetics", "College of Statics", "College of Dynamics", "College of Eternity" }, "College of Synergetics"),
    ("What is the name of the norn spirit of cunning?", new[] { "Raven", "Bear", "Wolf", "Snow Leopard" }, "Raven"),
    ("Which race has the Seraph as their guard?", new[] { "Human", "Charr", "Sylvari", "Asura" }, "Human"),
    ("What is the name of the sylvari who is a soundless ranger?", new[] { "Canach", "Caithe", "Trahearne", "Sayeh" }, "Canach"),
    ("Which expansion introduced the griffon mount?", new[] { "Path of Fire", "Living World Season 4", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("What is the name of the charr imperator of Iron Legion?", new[] { "Smodur the Unflinching", "Bangar Ruinbringer", "Malice Swordshadow", "Crecia" }, "Smodur the Unflinching"),
    ("Which god is associated with earth and growth?", new[] { "Melandru", "Dwayna", "Lyssa", "Grenth" }, "Melandru"),
    ("What is the name of the asura who is paralyzed but genius?", new[] { "Taimi", "Zojja", "Snaff", "Oola" }, "Taimi"),
    ("Which Elder Dragon was killed in Path of Fire?", new[] { "Kralkatorrik", "Balthazar", "Soo-Won", "Jormag" }, "Kralkatorrik"),
    ("What is the name of the norn spirit of stealth?", new[] { "Snow Leopard", "Bear", "Raven", "Wolf" }, "Snow Leopard"),
    ("Which race has the Vigil as headquarters?", new[] { "All races", "Human", "Charr", "Sylvari" }, "All races"),
    ("What is the name of the human centaur leader in Kryta?", new[] { "Ulgrim", "Ventari", "Palawa Joko", "Faolain" }, "Ulgrim"),
    ("What is the name of the charr imperator of Ash Legion?", new[] { "Malice Swordshadow", "Smodur", "Bangar", "Crecia" }, "Malice Swordshadow"),
    ("Which race has the Order of Whispers as secret order?", new[] { "All races", "Asura", "Sylvari", "Human" }, "All races"),
    ("What is the name of the sylvari first born who died protecting the Pale Tree?", new[] { "Riannoc", "Trahearne", "Caithe", "Faolain" }, "Riannoc"),
    ("Which expansion introduced the roller beetle mount?", new[] { "Living World Season 4", "Path of Fire", "End of Dragons", "Secrets of the Obscure" }, "Living World Season 4"),
    ("What is the name of the asura who pioneered golemancy?", new[] { "Oola", "Snaff", "Zojja", "Taimi" }, "Oola"),
    ("Which Elder Dragon was killed in Heart of Thorns?", new[] { "Mordremoth", "Zhaitan", "Kralkatorrik", "Jormag" }, "Mordremoth"),
    ("What is the name of the human minister who betrayed Kryta?", new[] { "Caudaule", "Anise", "Jennah", "Logan" }, "Caudaule"),
    ("Which expansion introduced the jackal mount?", new[] { "Path of Fire", "Living World Season 4", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("What is the name of the charr who wields Sohothin?", new[] { "Rytlock Brimstone", "Smodur", "Bangar", "Crecia" }, "Rytlock Brimstone"),
    ("Which god is associated with beauty and chaos?", new[] { "Lyssa", "Balthazar", "Kormir", "Grenth" }, "Lyssa"),
    ("What is the name of the norn spirit of the wild?", new[] { "Snow Leopard", "Bear", "Raven", "Wolf" }, "Snow Leopard"),
    ("What is the name of the human who became god of truth?", new[] { "Kormir", "Abaddon", "Balthazar", "Dwayna" }, "Kormir"),
    ("Which expansion introduced the skimmer mount?", new[] { "Path of Fire", "Living World Season 4", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("Which race has the Pact as main alliance?", new[] { "All races", "Human", "Charr", "Sylvari" }, "All races"),
    ("What is the name of the sylvari who purified Caladbolg?", new[] { "Trahearne", "The Pact Commander", "Caithe", "Canach" }, "Trahearne"),
    ("Which expansion introduced the warclaw in WvW?", new[] { "Living World Season 4", "Path of Fire", "End of Dragons", "Secrets of the Obscure" }, "Living World Season 4"),
    ("What is the name of the asura who built Blish's golem?", new[] { "Taimi", "Gorrik", "Zojja", "Snaff" }, "Taimi"),
    ("Which Elder Dragons was killed in Dragonstorm?", new[] { "Jormag and Primordus", "Soo-Won and Kralkatorrik" }, "Jormag and Primordus"),
    ("What is the name of the norn who is a guardian in story?", new[] { "Eir Stegalkin", "Braham", "Garm", "None" }, "Braham"),
    ("Which race has the Kryptis as enemies in Nayos?", new[] { "All races", "Human", "Asura", "Sylvari" }, "All races"),
    ("What is the name of the charr who is Crecia's son?", new[] { "Ryland Steelcatcher", "Bangar", "Smodur", "Rytlock" }, "Ryland Steelcatcher"),
    ("Which god is associated with fate and illusion?", new[] { "Lyssa", "Raven", "Kormir", "Grenth" }, "Lyssa"),
    ("What is the name of the asura who created the Dragon Bash hologram?", new[] { "Taimi", "Gorrik", "Zojja", "Snaff" }, "Taimi"),
    ("What is the name of the norn who wields a hammer?", new[] { "Braham Eirsson", "Eir Stegalkin", "Garm", "None" }, "Braham Eirsson"),
    ("Which race has the Astral Ward in Amnytas?", new[] { "All races", "Asura", "Human", "Sylvari" }, "All races"),
    ("What is the name of the sylvari who is a dragon champion turned ally?", new[] { "Canach", "Caithe", "Trahearne", "Faolain" }, "Canach"),
    ("Which expansion introduced the new legendary weapons in 2025?", new[] { "Visions of Eternity", "Secrets of the Obscure", "End of Dragons", "Path of Fire" }, "Visions of Eternity"),
    ("What is the name of the charr who is a gladium in story?", new[] { "Rox", "Rytlock", "Crecia", "Ryland" }, "Rox"),
    ("What is the name of the human who founded the White Mantle?", new[] { "Saul D'Alessio", "Caudaule", "Anise", "Jennah" }, "Saul D'Alessio"),
    ("What is the name of the asura who is a golemancer in Dragon's Watch?", new[] { "Taimi", "Gorrik", "Zojja", "None" }, "Taimi"),
    ("Which race has the Wizard's Tower as hub?", new[] { "All races", "Asura", "Human", "Sylvari" }, "All races"),
    ("What is the name of the sylvari who is a necromancer in story?", new[] { "Trahearne", "Caithe", "Canach", "Sayeh" }, "Trahearne"),
    ("What is the name of the charr who is a ranger in Dragon's Watch?", new[] { "Rox", "Rytlock", "Crecia", "Ryland" }, "Rox"),
    ("Which Elder Dragon was the crystal one?", new[] { "Kralkatorrik", "Jormag", "Primordus", "Zhaitan" }, "Kralkatorrik"),
    ("Which race has the Amnytas as sky location?", new[] { "All races", "Asura", "Human", "Sylvari" }, "All races"),
    ("What is the name of the charr who is a ranger in story?", new[] { "Rox", "Rytlock", "Crecia", "Ryland" }, "Rox"),
    ("Which Elder Dragon was the ice one?", new[] { "Jormag", "Zhaitan", "Primordus", "Kralkatorrik" }, "Jormag"),
    ("Which race has the Tower of Secrets as location?", new[] { "All races", "Asura", "Human", "Sylvari" }, "All races"),
    ("What is the name of the human who is a mesmer in story?", new[] { "Countess Anise", "Queen Jennah", "Logan Thackeray", "None" }, "Countess Anise"),
    ("Which expansion introduced the new story in 2025?", new[] { "Visions of Eternity", "Secrets of the Obscure", "End of Dragons", "Path of Fire" }, "Visions of Eternity"),
    ("Which Elder Dragon was the plant one?", new[] { "Mordremoth", "Zhaitan", "Kralkatorrik", "Jormag" }, "Mordremoth"),
    ("What is the name of the norn who is a ranger in story?", new[] { "Eir", "Braham", "Garm", "None" }, "Eir Stegalkin"),
    ("What is the name of the human who is a guardian in story?", new[] { "Logan", "None", "Anise", "Jennah" }, "Logan Thackeray"),
    ("Which expansion introduced the new elite specs in 2025?", new[] { "Visions of Eternity", "Secrets of the Obscure", "End of Dragons", "Path of Fire" }, "Visions of Eternity"),
    ("What is the name of the charr who is a revenant in story?", new[] { "Rytlock", "None", "Crecia", "Ryland" }, "Rytlock Brimstone"),
    ("Which god is associated with death and judgment?", new[] { "Grenth", "Jormag", "Dwayna", "Balthazar" }, "Grenth"),
    ("What is the name of the charr who is a warrior in story?", new[] { "Rytlock", "Bangar", "Smodur", "Ryland" }, "Rytlock Brimstone"),
    ("Which Elder Dragon was killed by Aurene in Path of Fire?", new[] { "Kralkatorrik", "Soo-Won", "Mordremoth", "Jormag" }, "Kralkatorrik"),
    ("What is the name of the sylvari first born who joined the Nightmare Court?", new[] { "Faolain", "Caithe", "Trahearne", "Riannoc" }, "Faolain"),
    ("What is the name of the asura college focused on temporal mechanics?", new[] { "College of Synergetics", "College of Dynamics", "College of Statics", "College of Eternity" }, "College of Synergetics"),
    ("Which expansion introduced the siege turtle mount?", new[] { "End of Dragons", "Path of Fire", "Secrets of the Obscure", "Visions of Eternity" }, "End of Dragons"),
    ("What is the name of the human centaur leader in Kryta?", new[] { "Ulgrim", "Ventari", "Palawa Joko", "None" }, "Ulgrim"),
    ("What is the name of the norn great lodge location?", new[] { "Hoelbrak", "Eye of the North", "Great Lodge", "Homestead" }, "Hoelbrak"),
    ("Which race has the Olmakhan as pacifists?", new[] { "Charr", "Human", "Sylvari", "Norn" }, "Charr"),
    ("What is the name of the asura who created the Dragon Bash hologram?", new[] { "Taimi", "Gorrik", "Zojja", "None" }, "Taimi"),
    ("What is the name of the norn who wields a hammer?", new[] { "Braham Eirsson", "Eir Stegalkin", "None", "Garm" }, "Braham Eirsson"),
    ("Which race has the Inner Nayos as demon realm?", new[] { "All races", "Kryptis", "Human", "Sylvari" }, "Kryptis"),
    ("What is the name of the norn who is a guardian in story?", new[] { "Eir Stegalkin", "Braham", "None", "Garm" }, "Eir Stegalkin"),
    ("Which race has the Amnytas as sky hub?", new[] { "All races", "Asura", "Human", "Sylvari" }, "All races"),
    ("What is the name of the sylvari who is a thief in story?", new[] { "Caithe", "Canach", "Trahearne", "Sayeh" }, "Caithe"),
    ("Which god is associated with illusion and fate?", new[] { "Lyssa", "Raven", "Kormir", "Grenth" }, "Lyssa"),
    ("Which Elder Dragon was the last to awaken?", new[] { "Soo-Won", "Zhaitan", "Mordremoth", "Primordus" }, "Soo-Won"),
    ("Which expansion introduced skiffs?", new[] { "End of Dragons", "Path of Fire", "Secrets of the Obscure", "Visions of Eternity" }, "End of Dragons"),
    ("Which Elder Dragon is associated with crystal and torment?", new[] { "Kralkatorrik", "Jormag", "Primordus", "Zhaitan" }, "Kralkatorrik"),
    ("What is the name of the norn lodge for hunters?", new[] { "Wolf Lodge", "Raven Lodge", "Bear Lodge", "Snow Leopard Lodge" }, "Wolf Lodge"),
    ("Which race was enslaved by the Mursaat?", new[] { "Tengu", "Largos", "Humans", "Dredge" }, "Humans"),
    ("What is the name of the sylvari second born who is a thief?", new[] { "Caithe", "Canach", "Trahearne", "Sayeh" }, "Caithe"),
    ("Which expansion introduced fishing?", new[] { "End of Dragons", "Secrets of the Obscure", "Path of Fire", "Visions of Eternity" }, "End of Dragons"),
    ("What is the name of the charr imperator of Blood Legion?", new[] { "Bangar Ruinbringer", "Smodur the Unflinching", "Malice Swordshadow", "Crecia Stoneglow" }, "Bangar Ruinbringer"),
    ("Which god is associated with beauty and illusion?", new[] { "Lyssa", "Dwayna", "Melandru", "Grenth" }, "Lyssa"),
    ("Which Elder Dragon is underground and fire?", new[] { "Primordus", "Jormag", "Zhaitan", "Soo-Won" }, "Primordus"),
    ("What is the name of the norn spirit of wisdom?", new[] { "Raven", "Bear", "Wolf", "Snow Leopard" }, "Raven"),
    ("Which race lives in the Grove?", new[] { "Sylvari", "Asura", "Norn", "Human" }, "Sylvari"),
    ("What is the name of the charr vehicle tanks?", new[] { "Iron Legion Charrzooka", "Charr Tanks", "Iron Legion Vehicles", "War Machines" }, "Iron Legion Charrzooka"),
    ("Which race has the Vigil order leader Almorra?", new[] { "Charr", "Norn", "Human", "Sylvari" }, "Charr"),
    ("What is the name of the asura college focused on stasis?", new[] { "College of Statics", "College of Dynamics", "College of Synergetics", "College of Arcane" }, "College of Statics"),
    ("Which race has the Priory order?", new[] { "All races", "Asura", "Human", "Norn" }, "All races"),
    ("What is the name of the human centaur war?", new[] { "Centaur War", "Human-Charr Conflict", "Orr War", "Krytan Civil War" }, "Centaur War"),
    ("Which expansion introduced turtle mount?", new[] { "End of Dragons", "Path of Fire", "Secrets of the Obscure", "Visions of Eternity" }, "End of Dragons"),
    ("What is the name of the norn homestead?", new[] { "Hoelbrak", "Great Lodge", "Homestead", "Lodges" }, "Great Lodge"),
    ("What is the name of the charr female imperator of Ash Legion?", new[] { "Malice Swordshadow", "Crecia Stoneglow", "Smodur the Unflinching", "Bangar Ruinbringer" }, "Malice Swordshadow"),
    ("Which race has the Dream of Dreams?", new[] { "Sylvari", "Asura", "Norn", "Charr" }, "Sylvari"),
    ("What is the name of the asura who worked with Destiny's Edge?", new[] { "Snaff", "Zojja", "Taimi", "Oola" }, "Snaff"),
    ("Which race has the Olmakhan tribe?", new[] { "Charr", "Human", "Sylvari", "Norn" }, "Charr"),
    ("What is the name of the charr Flame Legion leader?", new[] { "Gaheron Baelfire", "Bangar Ruinbringer", "Smodur", "Malice" }, "Gaheron Baelfire"),
    ("Which race has the Pale Tree as mother?", new[] { "Sylvari", "Asura", "Norn", "Human" }, "Sylvari"),
    ("What is the name of the asura college focused on eternity?", new[] { "College of Synergetics", "College of Statics", "College of Dynamics", "College of Eternity" }, "College of Synergetics"),
    ("Which race has the Seraph guard?", new[] { "Human", "Charr", "Sylvari", "Asura" }, "Human"),
    ("What is the name of the sylvari who is a soundless?", new[] { "Canach", "Caithe", "Trahearne", "Sayeh" }, "Canach"),
    ("Which expansion introduced griffon mount?", new[] { "Path of Fire", "Living World Season 4", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("What is the name of the charr Iron Legion imperator?", new[] { "Smodur the Unflinching", "Bangar Ruinbringer", "Malice Swordshadow", "Crecia" }, "Smodur the Unflinching"),
    ("What is the name of the asura who is in Dragon's Watch?", new[] { "Taimi", "Zojja", "Snaff", "Oola" }, "Taimi"),
    ("What is the name of the norn spirit of nature?", new[] { "Snow Leopard", "Bear", "Raven", "Wolf" }, "Snow Leopard"),
    ("Which race has the Vigil keep?", new[] { "All races", "Human", "Charr", "Sylvari" }, "All races"),
    ("What is the name of the human centaur leader?", new[] { "Ulgrim", "Ventari", "Palawa Joko", "None" }, "Ulgrim"),
    ("What is the name of the charr Ash Legion imperator?", new[] { "Malice Swordshadow", "Smodur", "Bangar", "Crecia" }, "Malice Swordshadow"),
    ("What is the name of the sylvari first born who died?", new[] { "Riannoc", "Trahearne", "Caithe", "Faolain" }, "Riannoc"),
    ("What is the name of the asura who created golemancy?", new[] { "Oola", "Snaff", "Zojja", "Taimi" }, "Oola"),
    ("What is the name of the human minister who was a villain?", new[] { "Caudaule", "Anise", "Jennah", "Logan" }, "Caudaule"),
    ("Which expansion introduced jackal mount?", new[] { "Path of Fire", "Living World Season 4", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("Which god is associated with chaos and beauty?", new[] { "Lyssa", "Balthazar", "Kormir", "Grenth" }, "Lyssa"),
    ("What is the name of the asura who is paralyzed?", new[] { "Taimi", "Zojja", "Snaff", "Oola" }, "Taimi"),
    ("Which race has the Olmakhan tribe as pacifists?", new[] { "Charr", "Human", "Sylvari", "Norn" }, "Charr"),
    ("Which expansion introduced skimmer mount?", new[] { "Path of Fire", "Living World Season 4", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("What is the name of the charr who is in Dragon's Watch?", new[] { "Rytlock Brimstone", "Crecia Stoneglow", "Bangar", "Smodur" }, "Rytlock Brimstone"),
    ("Which race has the Pact as alliance?", new[] { "All races", "Human", "Charr", "Sylvari" }, "All races"),
    ("What is the name of the sylvari who joined the Pact?", new[] { "Trahearne", "Caithe", "Canach", "Sayeh" }, "Trahearne"),
    ("Which expansion introduced beetle mount?", new[] { "Living World Season 4", "Path of Fire", "End of Dragons", "Visions of Eternity" }, "Living World Season 4"),
    ("What is the name of the asura who died in Claw Island?", new[] { "Snaff", "Zojja", "Taimi", "None" }, "Snaff"),
    ("Which Elder Dragon was killed in Living World Season 4?", new[] { "Kralkatorrik", "Jormag", "Primordus", "Soo-Won" }, "Kralkatorrik"),
    ("What is the name of the norn spirit of the bear?", new[] { "Bear", "Wolf", "Raven", "Snow Leopard" }, "Bear"),
    ("Which race has the Black Citadel as capital?", new[] { "Charr", "Asura", "Norn", "Human" }, "Charr"),
    ("Which expansion introduced the warclaw mount?", new[] { "Living World Season 4", "Path of Fire", "End of Dragons", "Secrets of the Obscure" }, "Living World Season 4"),
    ("What is the name of the asura who built the Dragonstorm weapon?", new[] { "Taimi", "Zojja", "Snaff", "Gorrik" }, "Taimi"),
    ("What is the name of the norn spirit of the raven?", new[] { "Raven", "Bear", "Wolf", "Snow Leopard" }, "Raven"),
    ("Which race has the Lionguard in Lion's Arch?", new[] { "All races", "Human", "Charr", "Sylvari" }, "All races"),
    ("What is the name of the asura who is Gorrik's brother?", new[] { "Blish", "Taimi", "Zojja", "None" }, "Blish"),
    ("What is the name of the norn spirit of the snow leopard?", new[] { "Snow Leopard", "Bear", "Raven", "Wolf" }, "Snow Leopard"),
    ("Which race has the Iron Legion focus on technology?", new[] { "Charr", "Asura", "Sylvari", "Human" }, "Charr"),
    ("What is the name of the human who is Countess Anise's bodyguard?", new[] { "Logan Thackeray", "None", "Canach", "Rytlock" }, "Logan Thackeray"),
    ("Which expansion introduced the springer mount?", new[] { "Path of Fire", "Heart of Thorns", "End of Dragons", "Secrets of the Obscure" }, "Path of Fire"),
    ("What is the name of the charr who betrayed the legions?", new[] { "Bangar Ruinbringer", "Rytlock", "Smodur", "Malice" }, "Bangar Ruinbringer"),
    ("What is the name of the sylvari who is a necromancer?", new[] { "Trahearne", "Caithe", "Canach", "None" }, "Trahearne"),
    ("Which expansion introduced the jade tech?", new[] { "End of Dragons", "Path of Fire", "Secrets of the Obscure", "Visions of Eternity" }, "End of Dragons"),
    ("What is the name of the asura who is in the Priory?", new[] { "Snaff", "Zojja", "Taimi", "Gorrik" }, "Gorrik"),
    ("What is the name of the norn who is in Dragon's Watch?", new[] { "Braham Eirsson", "Eir Stegalkin", "Garm", "None" }, "Braham Eirsson"),
    ("Which race has the Shining Blade?", new[] { "Human", "Sylvari", "Charr", "Asura" }, "Human"),
    ("Which expansion introduced the sky scale mount?", new[] { "Living World Season 4", "Path of Fire", "End of Dragons", "Secrets of the Obscure" }, "Living World Season 4"),
    ("What is the name of the charr who is Rytlock's son?", new[] { "Ryland", "None", "Crecia", "Bangar" }, "Ryland Steelcatcher"),
    ("What is the name of the asura who built Blish's golem body?", new[] { "Taimi", "Gorrik", "Zojja", "None" }, "Taimi"),
    ("What is the name of the norn who became a spirit?", new[] { "Eir Stegalkin", "Braham", "None", "Garm" }, "Eir Stegalkin"),
    ("Which race has the Arcane Council?", new[] { "Asura", "Sylvari", "Human", "Charr" }, "Asura"),
    ("What is the name of the sylvari who is a dragon champion?", new[] { "None", "Faolain", "Caithe", "Trahearne" }, "Faolain"),
    ("Which race has the Dream as birthplace?", new[] { "Sylvari", "Norn", "Asura", "Charr" }, "Sylvari"),
    ("Which race has the Inquest as villains?", new[] { "Asura", "Charr", "Sylvari", "Human" }, "Asura"),
    ("What is the name of the charr who is a Flame Legion shaman?", new[] { "Gaheron", "None", "Bangar", "Efi" }, "Gaheron Baelfire"),
    ("What is the name of the asura who is a genius child?", new[] { "Taimi", "None", "Zojja", "Gorrik" }, "Taimi"),
    ("What is the name of the human who is a mesmer?", new[] { "Countess Anise", "Queen Jennah", "Logan Thackeray", "None" }, "Countess Anise"),
    ("Which expansion introduced the legendary relics?", new[] { "Secrets of the Obscure", "End of Dragons", "Path of Fire", "Visions of Eternity" }, "Secrets of the Obscure"),
    ("Which race has the Pale Tree as protector?", new[] { "Sylvari", "Asura", "Norn", "Human" }, "Sylvari"),
    ("What is the name of the asura who is a golemancer?", new[] { "Snaff", "Zojja", "Taimi", "Oola" }, "Snaff"),
    ("What is the name of the norn who is a ranger?", new[] { "Eir Stegalkin", "Braham", "None", "Garm" }, "Eir Stegalkin"),
    ("Which race has the Wizards Tower?", new[] { "All races", "Asura", "Human", "Sylvari" }, "All races"),
    ("Which race has the Nightmare Court as villains?", new[] { "Sylvari", "Charr", "Asura", "Human" }, "Sylvari"),
    ("Which race has the Astral Ward as organization?", new[] { "All races", "Asura", "Human", "Sylvari" }, "All races"),
    ("What is the name of the sylvari who is a thief?", new[] { "Caithe", "Canach", "Trahearne", "Sayeh" }, "Caithe"),
    ("What is the name of the human who is a guardian?", new[] { "Logan Thackeray", "None", "Anise", "Jennah" }, "Logan Thackeray"),
    ("Which god is associated with illusion?", new[] { "Lyssa", "Raven", "Kormir", "Grenth" }, "Lyssa"),
    ("Which expansion introduced the new elite specs (2025)?", new[] { "Visions of Eternity", "Secrets of the Obscure", "End of Dragons", "Path of Fire" }, "Visions of Eternity"),
    ("Which Elder Dragon was the first to die?", new[] { "Zhaitan", "Mordremoth", "Kralkatorrik", "Jormag" }, "Zhaitan")

    
    };

    public async Task EnsureItemIdsLoaded() => _itemIds ??= await _http.GetFromJsonAsync<List<int>>("items") ?? new();
    private async Task EnsureSkillIdsLoaded() => _skillIds ??= await _http.GetFromJsonAsync<List<int>>("skills") ?? new();
    private async Task EnsureSpecIdsLoaded() => _specIds ??= await _http.GetFromJsonAsync<List<int>>("specializations") ?? new();
    private async Task EnsureProfessionsLoaded() => _professions ??= await _http.GetFromJsonAsync<List<string>>("professions") ?? new();
    public async Task EnsureColorsLoaded() => _colors ??= await _http.GetFromJsonAsync<List<Gw2Color>>("colors?ids=all") ?? new();

    public async Task<QuestionData> GenerateQuestion(string category)
    {
        category = category.ToLowerInvariant();
        if (category == "random")
        {
            var cats = new[] { "legendaries", "ranger pets", "crafting", "dyes", "collections", "skills", "elites", "maps", "lore" };
            category = cats[_rand.Next(cats.Length)];
        }

        return category switch
        {
            "legendaries" => await GenerateLegendaryTypeQuestion(),
            "ranger pets" => GenerateRangerPetQuestion(),
            "crafting" => GenerateCraftingQuestion(),
            "dyes" => await GenerateDyeHueQuestion(),
            //"collections" => GenerateCollectionQuestion(),
            "skills" => await GenerateSkillProfessionQuestion(),
            "elites" => await GenerateEliteProfessionQuestion(),
            "maps" => await GenerateMapQuestion(),
            "lore" => GenerateLoreQuestion(),
            _ => await GenerateLegendaryTypeQuestion() // Fallback
        };
    }

    /*private async Task<QuestionData> GenerateItemRarityQuestion()
    {
        await EnsureItemIdsLoaded();

        var selectedIds = _itemIds.OrderBy(_ => _rand.Next()).Take(10).ToList(); // More to find valid
        Gw2Item? correctItem = null;

        foreach (var batch in selectedIds.Chunk(50))
        {
            var items = await _http.GetFromJsonAsync<List<Gw2Item>>($"items?ids={string.Join(",", batch)}");
            correctItem = items?.FirstOrDefault(i => Rarities.Contains(i.rarity));
            if (correctItem != null) break;
        }

        if (correctItem == null)
        {
            return await GenerateSkillProfessionQuestion(); // Fallback
        }

        // All rarities as possible distractors
        var allRarities = Rarities.ToList();

        // Shuffle and take 3 distractors
        var distractors = allRarities
            .Where(r => r != correctItem.rarity)
            .OrderBy(_ => _rand.Next())
            .Take(3)
            .ToList();

        // Add correct and shuffle
        var optionsList = distractors.Append(correctItem.rarity).OrderBy(_ => _rand.Next()).ToArray();

        int correctIndex = Array.IndexOf(optionsList, correctItem.rarity);

        string questionText = $"What is the rarity of \"{correctItem.name}\"?";

        return new QuestionData("Items", questionText, optionsList, correctIndex, ((char)('A' + correctIndex)).ToString(), correctItem.rarity);
    }*/

    private async Task<QuestionData> GenerateSkillProfessionQuestion()
    {
        await EnsureSkillIdsLoaded();
        await EnsureProfessionsLoaded();

        Gw2Skill? correctSkill = null;
        string profession = string.Empty;
        int attempts = 0;

        while (correctSkill == null && attempts < 20)
        {
            attempts++;
            var candidateIds = _skillIds.OrderBy(_ => _rand.Next()).Take(50).ToList();
            var skills = await _http.GetFromJsonAsync<List<Gw2Skill>>($"skills?ids={string.Join(",", candidateIds)}");

            if (skills != null)
            {
                correctSkill = skills.FirstOrDefault(s => s.Professions != null && s.Professions.Count == 1);
                if (correctSkill != null)
                {
                    profession = correctSkill.Professions[0];
                }
            }
        }

       

        var shuffledProfs = _professionOptions.OrderBy(_ => _rand.Next()).Take(4).ToArray();

        if (correctSkill == null || string.IsNullOrWhiteSpace(profession))
        {
            return GenerateLoreQuestion();
        }

        if (!shuffledProfs.Contains(profession))
        {
            shuffledProfs[_rand.Next(4)] = profession;
        }

        int correctIndex = Array.IndexOf(shuffledProfs, profession);

        string questionText = $"Which profession has the skill \"{correctSkill.name}\"?";

        return new QuestionData("Skills", questionText, shuffledProfs, correctIndex, ((char)('A' + correctIndex)).ToString(), profession);
    }

    private async Task<QuestionData> GenerateEliteProfessionQuestion()
    {
        await EnsureSpecIdsLoaded();

        var specs = await _http.GetFromJsonAsync<List<Gw2Spec>>("specializations?ids=all");

        var eliteSpecs = specs?
            .Where(s => s.elite && !string.IsNullOrEmpty(s.profession))
            .ToList();

        if (eliteSpecs == null || eliteSpecs.Count == 0)
        {
            return  GenerateLoreQuestion(); // Fallback
        }

        var correctSpec = eliteSpecs[_rand.Next(eliteSpecs.Count)];

        // All professions
        string[] allProfs = _professionOptions;

        // Distractors: 3 random except correct
        var distractors = allProfs
            .Where(p => p != correctSpec.profession)
            .OrderBy(_ => _rand.Next())
            .Take(3)
            .ToList();

        // Options: distractors + correct, shuffle
        var options = distractors.Append(correctSpec.profession).OrderBy(_ => _rand.Next()).ToArray();

        int correctIndex = Array.IndexOf(options, correctSpec.profession);

        string questionText = $"Which profession has the elite specialization \"{correctSpec.name}\"?";

        return new QuestionData("Elites", questionText, options, correctIndex, ((char)('A' + correctIndex)).ToString(), correctSpec.profession);
    }

    private List<Gw2Map> _openWorldMaps = new();
    private List<string> _mainRegions = new();
    private List<(string LandmarkName, string MapName)> _landmarks = new();
    private HashSet<string> _openWorldMapNames = new();
    private bool _mapsDataLoaded = false;

    public async Task EnsureMapsDataLoaded()
    {
        if (_mapsDataLoaded) return;

        // Load maps for regions
        var allMaps = await _http.GetFromJsonAsync<List<Gw2Map>>("maps?ids=all") ?? new();

        var candidateMaps = allMaps
            .Where(m => m.type == "Public"
                        && m.min_level > 0
                        && !string.IsNullOrEmpty(m.region_name)
                        && !m.name.Contains("(")
                        && !m.name.Contains("Strike Mission", StringComparison.OrdinalIgnoreCase)
                        && !m.name.Contains("Fractal", StringComparison.OrdinalIgnoreCase)
                        && !m.name.Contains("Raid", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var mainRegionNames = candidateMaps
            .GroupBy(m => m.region_name)
            .Where(g => g.Count() >= 5)
            .Select(g => g.Key)
            .ToList();

        _openWorldMaps = candidateMaps
            .Where(m => mainRegionNames.Contains(m.region_name))
            .ToList();

        _mainRegions = mainRegionNames;

        _openWorldMapNames = _openWorldMaps.Select(m => m.name).ToHashSet();

        // Load landmarks from continents
        try
        {
            var floors = await _http.GetFromJsonAsync<List<int>>("continents/1/floors") ?? new();

            foreach (var floorId in floors)
            {
                var floorData = await _http.GetFromJsonAsync<Gw2FloorData>($"continents/1/floors/{floorId}");

                if (floorData == null) continue;

                foreach (var region in floorData.regions.Values)
                {
                    foreach (var map in region.maps.Values)
                    {
                        if (_openWorldMapNames.Contains(map.name))
                        {
                            if (map.points_of_interest != null)
                            {
                                foreach (var poi in map.points_of_interest.Values)
                                {
                                    if (poi.type == "landmark" && !string.IsNullOrEmpty(poi.name))
                                    {
                                        _landmarks.Add((poi.name, map.name));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            _landmarks = _landmarks.Distinct().OrderBy(_ => _rand.Next()).ToList();
        }
        catch
        {
            AppLogger.LogInfo("TriviaService.EnsureMapsDataLoaded", "Landmark pre-load failed. Falling back to region questions only.");
        }

        _mapsDataLoaded = true;
    }

    private async Task<QuestionData> GenerateMapQuestion()
    {
        await EnsureMapsDataLoaded();

        if (_openWorldMaps.Count == 0 || _mainRegions.Count == 0)
        {
            return GenerateLoreQuestion();
        }

        

        bool isLandmarkQuestion = _landmarks.Count > 0 && _rand.NextDouble() < 0.5;

        string[] options;
        int correctIndex;
        string correctFull;
        string questionText;

        if (isLandmarkQuestion)
        {
            var (landmarkName, correctMapName) = _landmarks[_rand.Next(_landmarks.Count)];

            var distractors = _openWorldMapNames
                .Where(n => n != correctMapName)
                .OrderBy(_ => _rand.Next())
                .Take(3)
                .ToList();

            options = distractors.Append(correctMapName).OrderBy(_ => _rand.Next()).ToArray();
            correctIndex = Array.IndexOf(options, correctMapName);
            correctFull = correctMapName;
            questionText = $"Which map is the landmark \"{landmarkName}\" located in?";
        }
        else
        {
            Gw2Map correctMap = _openWorldMaps[_rand.Next(_openWorldMaps.Count)];

            var shuffledRegions = _mainRegions.OrderBy(_ => _rand.Next()).Take(4).ToList();

            if (!shuffledRegions.Contains(correctMap.region_name))
            {
                shuffledRegions[_rand.Next(4)] = correctMap.region_name;
            }

            options = shuffledRegions.ToArray();
            correctIndex = Array.IndexOf(options, correctMap.region_name);
            correctFull = correctMap.region_name;
            questionText = $"Which region is the map \"{correctMap.name}\" in?";
        }

        return new QuestionData("Maps", questionText, options, correctIndex, ((char)('A' + correctIndex)).ToString(), correctFull);
    }

    private QuestionData GenerateLoreQuestion()
    {
        var (questionText, optionsList, correctFull) = _loreQuestions[_rand.Next(_loreQuestions.Count)];

        var options = optionsList.OrderBy(_ => _rand.Next()).ToArray();
        int correctIndex = Array.IndexOf(options, correctFull);

        return new QuestionData("Lore", questionText, options, correctIndex, ((char)('A' + correctIndex)).ToString(), correctFull);
    }

    // Fixed GenerateLegendaryTypeQuestion - Safe null check + fallback
    // No NullReferenceException
    private async Task<QuestionData> GenerateLegendaryTypeQuestion()
    {
        await EnsureItemIdsLoaded();

        Gw2Item? correctItem = null;
        int attempts = 0;

        while (correctItem == null && attempts < 30)
        {
            attempts++;
            var candidateIds = _itemIds.OrderBy(_ => _rand.Next()).Take(50).ToList();
            var items = await _http.GetFromJsonAsync<List<Gw2Item>>($"items?ids={string.Join(",", candidateIds)}");

            correctItem = items?.FirstOrDefault(i => i.rarity == "Legendary" &&
                                                    i.details?.type != null &&
                                                    (i.type == "Weapon" || i.type == "Trinket" || i.type == "Back"));
        }

        if (correctItem == null || correctItem.details?.type == null)
            return  GenerateLoreQuestion(); // Fallback

        string correctSpecific = correctItem.type == "Back" ? "Back item" : correctItem.details.type;

        // Complete list of all possible legendary specific types (from API/wiki)
        string[] commonTypes = {
        "Axe", "Dagger", "Greatsword", "Hammer", "Mace", "Sword", "Pistol", "Rifle", "Shield",
        "Focus", "Scepter", "Staff", "Torch", "Warhorn", "Longbow", "Shortbow",
        "Harpoon Gun", "Speargun", "Trident", // Underwater
        "Ring", "Amulet", "Accessory", "Earring", // Trinkets
        "Back item"
    };

        // Distractors: 3 random except correct
        var distractors = commonTypes
            .Where(t => t != correctSpecific)
            .OrderBy(_ => _rand.Next())
            .Take(3)
            .ToList();

        // Options: distractors + correct, shuffle
        var options = distractors.Append(correctSpecific).OrderBy(_ => _rand.Next()).ToArray();

        int correctIndex = Array.IndexOf(options, correctSpecific);

        string questionText = $"What specific type is the legendary \"{correctItem.name}\"?";

        return new QuestionData("Legendaries", questionText, options, correctIndex, ((char)('A' + correctIndex)).ToString(), correctSpecific);
    }
    /*private async Task<QuestionData> GenerateLegendaryTypeQuestion()
    {
        await EnsureItemIdsLoaded();

        Gw2Item? correctItem = null;
        int attempts = 0;

        while (correctItem == null && attempts < 30)
        {
            attempts++;
            var candidateIds = _itemIds.OrderBy(_ => _rand.Next()).Take(50).ToList();
            var items = await _http.GetFromJsonAsync<List<Gw2Item>>($"items?ids={string.Join(",", candidateIds)}");

            if (items != null)
            {
                correctItem = items.FirstOrDefault(i => i.rarity == "Legendary" &&
                                                        (i.type == "Weapon" || i.type == "Trinket"));
            }
        }

        if (correctItem == null)
        {
            // Safe fallback - no crash
            return  GenerateLoreQuestion(); // Or any other category
        }

        string correctType = correctItem.type == "Weapon" ? "Weapon" : "Trinket";

        string[] types = { "Weapon", "Trinket", "Back item", "Armor" };
        var shuffled = types.OrderBy(_ => _rand.Next()).ToArray();

        if (!shuffled.Contains(correctType))
        {
            shuffled[_rand.Next(4)] = correctType;
        }

        int correctIndex = Array.IndexOf(shuffled, correctType);

        string questionText = $"Is the legendary \"{correctItem.name}\" a weapon or trinket?";

        return new QuestionData("Legendaries", questionText, shuffled, correctIndex, ((char)('A' + correctIndex)).ToString(), correctType);
    }*/

    private readonly Dictionary<string, string> _craftingTypes = new()
{
    // Weaponsmith (heavy weapons)
    {"Axe", "Weaponsmith"},
    {"Dagger", "Weaponsmith"},
    {"Greatsword", "Weaponsmith"},
    {"Hammer", "Weaponsmith"},
    {"Mace", "Weaponsmith"},
    {"Sword", "Weaponsmith"},
    {"Spear", "Weaponsmith"},
    {"Shield", "Weaponsmith"},

    // Huntsman (ranged + offhand)
    {"Longbow", "Huntsman"},
    {"Shortbow", "Huntsman"},
    {"Rifle", "Huntsman"},
    {"Torch", "Huntsman"},
    {"Warhorn", "Huntsman"},
    {"Harpoon Gun", "Huntsman"},
    {"Pistol", "Huntsman"},

    // Artificer (magic weapons)
    {"Focus", "Artificer"},
    {"Scepter", "Artificer"},
    {"Staff", "Artificer"},
    {"Trident", "Artificer"},

    // Armorsmith (heavy armor)
    {"Heavy Helm", "Armorsmith"},
    {"Heavy Shoulders", "Armorsmith"},
    {"Heavy Coat", "Armorsmith"},
    {"Heavy Gloves", "Armorsmith"},
    {"Heavy Leggings", "Armorsmith"},
    {"Heavy Boots", "Armorsmith"},

    // Leatherworker (medium armor)
    {"Medium Mask", "Leatherworker"},
    {"Medium Shoulders", "Leatherworker"},
    {"Medium Coat", "Leatherworker"},
    {"Medium Gloves", "Leatherworker"},
    {"Medium Pants", "Leatherworker"},
    {"Medium Boots", "Leatherworker"},

    // Tailor (light armor)
    {"Light Mask", "Tailor"},
    {"Light Mantle", "Tailor"},
    {"Light Coat", "Tailor"},
    {"Light Gloves", "Tailor"},
    {"Light Leggings", "Tailor"},
    {"Light Boots", "Tailor"},

    // Jeweler (trinkets)
    {"Ring", "Jeweler"},
    {"Amulet", "Jeweler"},
    {"Accessory", "Jeweler"},
    {"Earring", "Jeweler"},

    // Chef (food)
    {"Feast", "Chef"},
    {"Soup", "Chef"},
    {"Dessert", "Chef"},
    {"Snack", "Chef"},
    {"Meal", "Chef"},
    {"Ingredient", "Chef"}
};

    private QuestionData GenerateCraftingQuestion()
    {
        var kvp = _craftingTypes.OrderBy(_ => _rand.Next()).First();

        // All 9 active disciplines
        string[] allDisciplines = { "Weaponsmith", "Huntsman", "Artificer", "Armorsmith", "Leatherworker", "Tailor", "Jeweler", "Chef", "Scribe" };

        // Distractors: 3 random except correct
        var distractors = allDisciplines
            .Where(d => d != kvp.Value)
            .OrderBy(_ => _rand.Next())
            .Take(3)
            .ToList();

        // Full options: correct + distractors
        var optionsList = distractors.Append(kvp.Value).OrderBy(_ => _rand.Next()).ToArray();

        int correctIndex = Array.IndexOf(optionsList, kvp.Value);

        string questionText = $"Which crafting discipline makes {kvp.Key}s?";

        return new QuestionData("Crafting", questionText, optionsList, correctIndex, ((char)('A' + correctIndex)).ToString(), kvp.Value);
    }

    // 4. Dyes (dynamic hue)




    private async Task<QuestionData> GenerateDyeHueQuestion()
    {
        await EnsureColorsLoaded();

        if (_colors == null || _colors.Count == 0)
        {
            return GenerateLoreQuestion();
        }

        var color = _colors[_rand.Next(_colors.Count)];

        string correctHue = color.categories.FirstOrDefault() ?? "Unknown";

        // Full hues for better variety
        string[] allHues = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Brown", "Gray" };

        // Shuffle all, take 3 distractors
        var distractors = allHues
            .Where(h => h != correctHue)
            .OrderBy(_ => _rand.Next())
            .Take(3)
            .ToList();

        // Add correct + shuffle
        var optionsList = distractors.Append(correctHue).OrderBy(_ => _rand.Next()).ToArray();

        int correctIndex = Array.IndexOf(optionsList, correctHue);

        string questionText = $"Which main hue category does the dye \"{color.name}\" belong to?";

        return new QuestionData("Dyes", questionText, optionsList, correctIndex, ((char)('A' + correctIndex)).ToString(), correctHue);
    }

    public record Gw2Color(string name, List<string> categories);


    private readonly Dictionary<string, List<string>> _rangerPetMaps = new()
    {
    // Aether Hunter
    {"Juvenile Aether Hunter", new List<string> {"Amnytas" }},

    // Armor Fish
    {"Juvenile Armor Fish", new List<string> {"Blazeridge Steppes", "Lion's Arch", "Bloodtide Coast", "Mount Maelstrom", "Eternal Battlegrounds" }},

    // Arctodus
    {"Juvenile Arctodus", new List<string> {"Wayfarer Foothills", "Lornar's Pass", "Frostgorge Sound" }},

    // Black Bear
    {"Juvenile Black Bear", new List<string> {"Blazeridge Steppes", "Eternal Battlegrounds", "Desert Borderlands" }},

    // Brown Bear
    {"Juvenile Brown Bear", new List<string> {"Fields of Ruin", "Gendarran Fields", "Harathi Hinterlands", "Eternal Battlegrounds" }},

    // Murellow
    {"Juvenile Murellow", new List<string> {"Brisban Wildlands", "Mount Maelstrom", "Dredgehaunt Cliffs" }},

    // Polar Bear
    {"Juvenile Polar Bear", new List<string> {"Hoelbrak", "Frostgorge Sound" }},

    // Janthiri Bee
    {"Juvenile Janthiri Bee", new List<string> {"Mistburned Barrens" }},

    // Eagle
    {"Juvenile Eagle", new List<string> {"Fields of Ruin", "Bloodtide Coast", "Gendarran Fields", "Harathi Hinterlands", "Kessex Hills", "Timberline Falls" }},

    // Hawk
    {"Juvenile Hawk", new List<string> {"Iron Marches" }},

    // Owl
    {"Juvenile Owl", new List<string> {"Fireheart Rise", "Snowden Drifts", "Dredgehaunt Cliffs", "Frostgorge Sound" }},

    // Raven
    {"Juvenile Raven", new List<string> {"Fields of Ruin", "Gendarran Fields", "Hoelbrak", "Lornar's Pass", "Wayfarer Foothills" }},

    // White Raven
    {"Juvenile White Raven", new List<string> {"Hall of Monuments" }},

    // Alpine Wolf
    {"Juvenile Alpine Wolf", new List<string> {"Hoelbrak", "Lornar's Pass",  "Timberline Falls" }},

    {"Juvenile Wolf", new List<string> { "Eternal Battlegrounds", "Red Desert Borderlands"}},

    // Fern Hound
    {"Juvenile Fern Hound", new List<string> {"Fields of Ruin", "Brisban Wildlands", "Caledon Forest", "Mount Maelstrom", "Sparkfly Fen", "The Grove", "Straits of Devastation" }},

    // Hyena
    {"Juvenile Hyena", new List<string> {"Fields of Ruin", "Blazeridge Steppes", "Desert Borderlands" }},

    // Krytan Drakehound
    {"Juvenile Krytan Drakehound", new List<string> {"Fields of Ruin", "Divinity's Reach", "Lion's Arch", "Gendarran Fields", "Straits of Devastation" }},

    // Sky-Chak Striker
    {"Juvenile Sky-Chak Striker", new List<string> {"Skywatch Archipelago" }},

    // Spinegazer
    {"Juvenile Spinegazer", new List<string> {"Inner Nayos " }},

    // Lashtail Devourer
    {"Juvenile Lashtail Devourer", new List<string> {"Black Citadel", "Blazeridge Steppes", "Diessa Plateau", "Straits of Devastation" }},

    // Whiptail Devourer
    {"Juvenile Whiptail Devourer", new List<string> {"Black Citadel", "Fields of Ruin", "Plains of Ashford", "Eternal Battlegrounds", "Red Desert Borderlands" }},

    // Ice Drake
    {"Juvenile Ice Drake", new List<string> {"Dredgehaunt Cliffs", "Lornar's Pass", "Snowden Drifts", "Timberline Falls", "Wayfarer Foothills", "Frostgorge Sound"}},

    // Marsh Drake
    {"Juvenile Marsh Drake", new List<string> {"Caledon Forest", "Sparkfly Fen", "Mount Maelstrom", "Eternal Battlegrounds" }},

    // Reef Drake
    {"Juvenile Reef Drake", new List<string> {"Southsun Cove" }},

    // River Drake
    {"Juvenile River Drake", new List<string> {"Diessa Plateau", "Kessex Hills", "Eternal Battlegrounds", "Blue Alpine Borderlands", "Green Alpine Borderlands", "Kessex Hills", "Gendarran Fields", "Bloodtide Coast" }},

    // Salamander Drake
    {"Juvenile Salamander Drake", new List<string> {"Blazeridge Steppes", "Iron Marches", "Fireheart Rise", "Eternal Battlegrounds" }},

    // Cheetah
    {"Juvenile Cheetah", new List<string> {"Elon Riverlands" }},

    // Jaguar
    {"Juvenile Jaguar", new List<string> {"Brisban Wildlands", "Eternal Battlegrounds" }},

    // Sand Lion
    {"Juvenile Sand Lion", new List<string> {"Crystal Oasis" }},

    // Lynx
    {"Juvenile Lynx", new List<string> {"Fields of Ruin", "Lion's Arch", "Dredgehaunt Cliffs", "Snowden Drifts", "Red Desert Borderlands" }},

    // Snow Leopard
    {"Juvenile Snow Leopard", new List<string> {"Hoelbrak", "Frostgorge Sound", "Lornar's Pass", "Blue Alpine Borderlands", "Green Alpine Borderlands" }},

    // Tiger
    {"Juvenile Tiger", new List<string> {"Dragon's Stand", "Draconis Mons" }},

    // White Tiger
    {"Juvenile White Tiger", new List<string> {"Seitung Province" }},

    // Shark
    {"Juvenile Shark", new List<string> {"Bloodtide Coast", "Kessex Hills", "Lion's Arch", "Sparkfly Fen", "Mount Maelstrom", "Timberline Falls", "Blue Alpine Borderlands", "Green Alpine Borderlands" }},

    // Blue Jellyfish
    {"Juvenile Blue Jellyfish", new List<string> {"Lion's Arch", "Mount Maelstrom", "Frostgorge Sound", "Dragon's End", "Lowland Shore", "Blue Alpine Borderlands", "Green Alpine Borderlands", "Eternal Battlegrounds" }},

    // Red Jellyfish
    {"Juvenile Red Jellyfish", new List<string> {"Bloodtide Coast", "Rata Sum", "Mount Maelstrom", "Dragon's End", "Lowland Shore" }},

    // Rainbow Jellyfish
    {"Juvenile Rainbow Jellyfish", new List<string> {"Hall of Monuments" }},

    // Fanged Iboga
    {"Juvenile Fanged Iboga", new List<string> {"Desert Highlands" }},

    // Jacaranda
    {"Juvenile Jacaranda", new List<string> {"Domain of Vabbi" }},

    // Black Moa
    {"Juvenile Black Moa", new List<string> {"Hall of Monuments" }},

    // Blue Moa
    {"Juvenile Blue Moa", new List<string> { "Fields of Ruin", "Bloodtide Coast", "Caledon Forest", "Straits of Devastation" }},

    // Pink Moa
    {"Juvenile Pink Moa", new List<string> {"Brisban Wildlands", "Caledon Forest", "Mount Maelstrom", "Rata Sum", "Dry Top", "Lion's Arch", "The Grove" }},

    // Red Moa
    {"Juvenile Red Moa", new List<string> {"Blazeridge Steppes", "Fields of Ruin", "Fireheart Rise", "Eternal Battlegrounds)", "Red Desert Borderlands" }},

    // White Moa
    {"Juvenile White Moa", new List<string> {"Frostgorge Sound", "Lornar's Pass", "Snowden Drifts" }},

    // Boar
    {"Juvenile Boar", new List<string> {"Straits of Devastation", "Eternal Battlegrounds", "Blue Alpine Borderlands", "Green Alpine Borderlands", "Red Desert Borderlands" }},

    // Pig
    {"Juvenile Pig", new List<string> {"Fields of Ruin", "Divinity's Reach", "Gendarran Fields", "Lion's Arch", "Straits of Devastation", "Blue Alpine Borderlands", "Green Alpine Borderlands" }},

    // Siamoth
    {"Juvenile Siamoth", new List<string> {"Brisban Wildlands", "Rata Sum", "Sparkfly Fen" }},

    // Warthog
    {"Juvenile Warthog", new List<string> {"Diessa Plateau", "Fields of Ruin", "Fireheart Rise", "Auric Basin", "Gendarran Fields", "Eternal Battlegrounds", "Red Desert Borderlands" }},

    // Wallow
    {"Juvenile Wallow", new List<string> {"The Echovald Wilds" }},

    // Bristleback
    {"Juvenile Bristleback", new List<string> {"Auric Basin" }},

    // Smokescale
    {"Juvenile Smokescale", new List<string> {"Tangled Depths" }},

    // Rock Gazelle
    {"Juvenile Rock Gazelle", new List<string> {"The Desolation" }},

    // Electric Wyvern
    {"Juvenile Electric Wyvern", new List<string> {"Dragon's Stand" }},

    // Fire Wyvern
    {"Juvenile Fire Wyvern", new List<string> {"Verdant Brink" }},

    // Siege Turtle
    {"Juvenile Siege Turtle", new List<string> {"Dragon's End)" }},

    // Warclaw
    {"Juvenile Warclaw", new List<string> {"Lowland Shore" }},

    // Phoenix
    {"Juvenile Phoenix", new List<string> {"New Kaineng City"}},

    // Black Widow Spider
    {"Juvenile Black Widow Spider", new List<string> {"Hall of Monuments" }},

    // Cave Spider
    {"Juvenile Cave Spider", new List<string> {"Harathi Hinterlands", "Lornar's Pass", "Timberline Falls", "Red Desert Borderlands" }},

    // Forest Spider
    {"Juvenile Forest Spider", new List<string> {"Iron Marches", "Eternal Battlegrounds", "Blue Alpine Borderlands", "Green Alpine Borderlands" }},

    // Jungle Spider
    {"Juvenile Jungle Spider", new List<string> {"Caledon Forest", "Brisban Wildlands", "Sparkfly Fen", "Straits of Devastation" }},

    // Carrion Devourer
    {"Juvenile Carrion Devourer", new List<string> {"Fireheart Rise", "Iron Marches" }},

    // Raptor Swiftwing
    {"Juvenile Raptor Swiftwing", new List<string> {"Starlit Weald" }},

    
    {"Juvenile Jungle Stalker", new List<string> {"Caledon Forest", "Mount Maelstrom" }},

    
    };

    private QuestionData GenerateRangerPetQuestion()
    {
        var petKeys = _rangerPetMaps.Keys.ToList();
        string pet = petKeys[_rand.Next(petKeys.Count)];

        var petMaps = _rangerPetMaps[pet];
        string correctMap = petMaps[_rand.Next(petMaps.Count)];

        // Common distractor maps (popular taming zones)
        string[] commonMaps = { "Queensdale", "Wayfarer Foothills", "Metrica Province", "Verdant Brink", "Dredgehaunt Cliffs", "Divinity's Reach", "Tangled Depths", "The Echovald Wilds", "Harathi Hinterlands", "Rata Sum", "Fireheart Rise", "Iron Marches", "Southsun Cove", "Elon Riverlands", "Dragon's Stand", "Draconis Mons", "Auric Basin", "Desert Highlands", "Hall of Monuments", "Brisban Wildlands", "Snowden Drifts", "Lornar's Pass", "Dry Top", "Dragon's End", "New Kaineng City", "Amnytas", "Lowland Shore", "The Desolation", "Skywatch Archipelago", "Inner Nayos", "Black Citadel",  "Frostgorge Sound", "Caledon Forest", "Sparkfly Fen", "Straits of Devastation", "Blazeridge Steppes", "Lion's Arch", "Bloodtide Coast", "Mount Maelstrom", "The Grove", "Starlit Weald", "Seitung Province", "Domain of Vabbi", "Eternal Battlegrounds","Blue Alpine Borderlands", "Green Alpine Borderlands", "Red Desert Borderlands" };

        // Exclude pet's maps from distractors (no multiple correct)
        var availableDistractors = commonMaps.Where(m => !petMaps.Contains(m)).ToList();

        // Fallback if too few
        if (availableDistractors.Count < 3)
        {
            availableDistractors = commonMaps.Where(m => m != correctMap).ToList();
        }

        var distractors = availableDistractors.OrderBy(_ => _rand.Next()).Take(3).ToList();

        var optionsList = distractors.Append(correctMap).OrderBy(_ => _rand.Next()).ToList();
        string[] options = optionsList.ToArray();

        int correctIndex = Array.IndexOf(options, correctMap);

        string questionText = $"In which map can rangers tame the \"{pet}\"?";

        return new QuestionData("Ranger Pets", questionText, options, correctIndex, ((char)('A' + correctIndex)).ToString(), correctMap);
    }
}

public record Gw2Item(int id, string name, string rarity, string type, Gw2ItemDetails? details);
public record Gw2ItemDetails(string? type);
public record Gw2Skill(
    int id,
    string name,
    [property: JsonPropertyName("professions")] List<string>? Professions
);
public record Gw2Color(string name, List<string> categories);
public record Gw2Spec(
    int id,
    string name,
    string profession,
    [property: JsonPropertyName("elite")] bool elite
);

public record Gw2Poi(
    [property: JsonPropertyName("type")] string type,
    [property: JsonPropertyName("name")] string name
);

public record Gw2MapDetail(
    string name,
    string type,
    int min_level,
    [property: JsonPropertyName("points_of_interest")] Dictionary<int, Gw2Poi>? points_of_interest
);

public record Gw2Region(
    [property: JsonPropertyName("maps")] Dictionary<int, Gw2MapDetail> maps
);

public record Gw2FloorData(
    [property: JsonPropertyName("regions")] Dictionary<int, Gw2Region> regions
);

public record Gw2Map(
    int id,
    string name,
    [property: JsonPropertyName("region_name")] string region_name,
    [property: JsonPropertyName("type")] string type,
    [property: JsonPropertyName("min_level")] int min_level
);