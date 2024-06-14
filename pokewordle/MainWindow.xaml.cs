using Microsoft.VisualBasic.FileIO;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;

namespace pokewordle
{
    /// <summary>
    /// An app to facilitate a wordle style pokemon challenge
    /// the challenge is to play and beat a pokemon game with a specific party that this app generates
    /// the catch is you are only allowed to guess when you beat a gym/elite 4
    /// although the app has no way of enforcing this condition, so it's up to you when you check if you figred out the party
    /// 
    /// the secret party will never have mons that are unavailable in the chosen game
    /// the secret party will not have duplicates 
    /// (though it is possible for the secret party to include multiple evolutions from the same evolutionary line)
    /// the secret party can only have legendries if the checkbox was checked during party generation
    /// 
    /// when making a guess, the app will check each selected mon against the secret party
    /// for each selected mon, the app will first check if they match one of the mons in the secret party
    /// if any match is found then that slot is marked green and that mon is excluded from further checks
    /// the app will then check eatch slot and count the number of times the type(s) that slot appears in the secret party
    /// the app will then report the total count for each type in each slot
    /// 
    /// there is special logic for handling a mon that is part of a mutually exclusive group
    ///     a mutually exclusive group is a set of pokemon in a given game where it is only possible to ever have one pokemon in that set without trading
    ///     for example, in gen 1 there is only 1 eevee available and breeding is not available
    ///     therefore it is impossible for a party to have more than 1 eeveelution in gen 1
    ///
    /// for mutually exclusive groups
    ///      - during party generation the app will only allow 1 mon from any given exlcusive group to be in the party
    ///        because it considers all mons within a mutually exclusive group to be equivalent
    ///        
    ///      - when guessing, if one of the slots guessed is part of a mutually exclusive group
    ///        and a member of that group is in the secret party
    ///        then that slot is immediately marked as a match
    ///        
    /// so if you're playing gen 1,
    /// if you guess guess eevee is in the party and any eeveelution is in the secret party, 
    /// then it will be marked as a match
    /// if you guess eevee and no eeveelutions are in the secret party then the usual logic applies
    /// the app will count the total normal types in the secret party, and the number of mons without a secondary type in the secret party
    /// 
    /// 
    /// pokemon_dataset.csv is derived from the dataset found at https://www.kaggle.com/datasets/ceebloop/pokmon-dataset-2024
    /// </summary>

    public partial class MainWindow : Window
    {
        string saveFilePath = "";
        List<Pokemon> secretParty = new List<Pokemon>();
        List<Pokemon> pokedex = new List<Pokemon>();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btn_generate_Click(object sender, RoutedEventArgs e)
        {
            if (secretParty.Count > 0)
            {
                string messageBoxText = "A party was found in the save file.\nAre you sure you want to overwrite the current party?";
                string caption = "Generate Party";
                MessageBoxButton button = MessageBoxButton.YesNoCancel;
                MessageBoxImage icon = MessageBoxImage.Warning;
                MessageBoxResult result;

                result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

                if (result == MessageBoxResult.Yes)
                {
                    if (cb_game.SelectedIndex < 0)
                    {
                        string gameMessageBoxText = "Please select a game before generating a party.";
                        string c = "Select Game";
                        MessageBoxButton b = MessageBoxButton.OK;
                        MessageBoxImage i = MessageBoxImage.Warning;
                        MessageBox.Show(gameMessageBoxText, c, b, i, MessageBoxResult.OK);
                    }
                    else
                    {
                        generateDex();
                        secretParty.Clear();
                        secretParty = generateParty();
                        Save(secretParty);
                    }
                }
            }
            else
            {
                if (cb_game.SelectedIndex < 0)
                {
                    string gameMessageBoxText = "Please select a game before generating a party.";
                    string c = "Select Game";
                    MessageBoxButton b = MessageBoxButton.OK;
                    MessageBoxImage i = MessageBoxImage.Warning;
                    MessageBox.Show(gameMessageBoxText, c, b, i, MessageBoxResult.OK);
                }
                else
                {
                    generateDex();
                    secretParty.Clear();
                    secretParty = generateParty();
                    Save(secretParty);
                }
            }
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            saveFilePath = Microsoft.VisualBasic.FileSystem.CurDir() + "\\Resources\\savefile";
            try
            {
                secretParty.Clear();
                secretParty = Load();
                cb_game.SelectedIndex = secretParty[0].game;
                generateDex();
            }
            catch (Exception)
            {
                //couldnt load party
                File.Delete(saveFilePath);
                File.WriteAllText(saveFilePath, null);
                secretParty.Clear();
                cb_game.SelectedIndex = 0;
                generateDex();
            }
        }
        private List<Pokemon> Load()
        {
            string file = saveFilePath;
            List<Pokemon> listofa = new List<Pokemon>();
            XmlSerializer formatter = new XmlSerializer(typeof(List<Pokemon>));
            FileStream aFile = new FileStream(file, FileMode.Open);
            byte[] buffer = new byte[aFile.Length];
            aFile.Read(buffer, 0, (int)aFile.Length);
            MemoryStream stream = new MemoryStream(buffer);
            return (List<Pokemon>)formatter.Deserialize(stream);
        }

        private void Save(List<Pokemon> listofmons)
        {
            string path = saveFilePath;
            FileStream outFile = File.Create(path);
            XmlSerializer formatter = new XmlSerializer(typeof(List<Pokemon>));
            formatter.Serialize(outFile, listofmons);
        }

        private void generateDex()
        {
            pokedex.Clear();
            using (TextFieldParser parser = new TextFieldParser(Microsoft.VisualBasic.FileSystem.CurDir() + "\\Resources\\pokemon_dataset.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                string[] header = parser.ReadFields();
                string[] regions = header.Skip(10).ToArray();
                foreach (string region in regions) 
                { 
                    cb_game.Items.Add(region);
                }
                while (!parser.EndOfData)
                {
                    //Process row
                    string[] fields = parser.ReadFields();
                    int d = Convert.ToInt32(fields[0]);
                    string species = fields[1];
                    string firstType = fields[2];
                    string secondType = fields[3];
                    string firstAbil = fields[4];
                    string secondAbil = fields[5];
                    string hiddenAbil = fields[6];
                    bool legend = Convert.ToBoolean(fields[7]);
                    List<int> alts = new List<int>();

                    //fields[8 + game] is the column denoting the restrictions for the selected region's 
                    if (fields[10 + cb_game.SelectedIndex].Contains("unavailable"))
                    {//if marked unavailable then skip adding the mon
                        continue;
                    }
                    else if (fields[10 + cb_game.SelectedIndex] == "")
                    {//if no note is added then this species requires strict equality
                        alts.Add(d);
                    }
                    else
                    {//if a list is given then it is because that species is mutually exclusive to others in that gen
                        //the list denotes what other dex values will be considered equivalent
                        alts = fields[10 + cb_game.SelectedIndex].Split(';').Select(Int32.Parse).ToList();
                    }
                    Pokemon mon = new Pokemon(d, species, firstType, secondType, firstAbil, secondAbil, hiddenAbil, legend, alts, cb_game.SelectedIndex);
                    pokedex.Add(mon);
                }
            }
            populateSlots();
        }
        private void populateSlots()
        {
            cb_Slot1.Items.Clear();
            cb_Slot2.Items.Clear();
            cb_Slot3.Items.Clear();
            cb_Slot4.Items.Clear();
            cb_Slot5.Items.Clear();
            cb_Slot6.Items.Clear();
            foreach (Pokemon mon in pokedex)
            {
                cb_Slot1.Items.Add(mon.name);
                cb_Slot2.Items.Add(mon.name);
                cb_Slot3.Items.Add(mon.name);
                cb_Slot4.Items.Add(mon.name);
                cb_Slot5.Items.Add(mon.name);
                cb_Slot6.Items.Add(mon.name);
            }
        }
        private List<Pokemon> generateParty()
        {
            gb_Slot1.Background = Brushes.Transparent;
            gb_Slot2.Background = Brushes.Transparent;
            gb_Slot3.Background = Brushes.Transparent;
            gb_Slot4.Background = Brushes.Transparent;
            gb_Slot5.Background = Brushes.Transparent;
            gb_Slot6.Background = Brushes.Transparent;

            List<Pokemon> randoParty = new List<Pokemon>();
            for (int i = 0; i < 6; i++)
            {
                var random = new Random();
                int index = random.Next(pokedex.Count());
                Pokemon randoMon = pokedex[index];
                while (randoParty.Count != 0 && (randoParty.Contains(randoMon) || randoParty.All(x => x.acceptableAlts.Contains(randoMon.dex)) || (randoMon.legendary && ck_legendries.IsChecked == false)))
                {//roll again if the mon is already in the party, or if the mon is considered an acceptable alt to a mon in the party, or is legendary
                    index = random.Next(pokedex.Count());
                    randoMon = pokedex[index];
                }
                randoParty.Add(randoMon);
            }
            return randoParty;
        }

        private void btn_CheckParty_Click(object sender, RoutedEventArgs e)
        {
            if (cb_Slot1.SelectedIndex == -1 || cb_Slot2.SelectedIndex == -1 || cb_Slot3.SelectedIndex == -1 || cb_Slot4.SelectedIndex == -1 || cb_Slot5.SelectedIndex == -1 || cb_Slot6.SelectedIndex == -1)
            {
                string gameMessageBoxText = "Please fill out the party before checking.";
                string c = "Check Party";
                MessageBoxButton b = MessageBoxButton.OK;
                MessageBoxImage i = MessageBoxImage.Warning;
                MessageBox.Show(gameMessageBoxText, c, b, i, MessageBoxResult.OK);
            }
            else if (secretParty.Count < 6)
            {
                string gameMessageBoxText = "Please generate a party before guessing.";
                string c = "Check Party";
                MessageBoxButton b = MessageBoxButton.OK;
                MessageBoxImage i = MessageBoxImage.Warning;
                MessageBox.Show(gameMessageBoxText, c, b, i, MessageBoxResult.OK);
            }
            else
            {
                gb_Slot1.Background = Brushes.Transparent;
                gb_Slot2.Background = Brushes.Transparent;
                gb_Slot3.Background = Brushes.Transparent;
                gb_Slot4.Background = Brushes.Transparent;
                gb_Slot5.Background = Brushes.Transparent;
                gb_Slot6.Background = Brushes.Transparent;
                List<Pokemon> temp = new List<Pokemon>(secretParty);

                int slot1_typeA_Match = 0;
                int slot1_typeB_Match = 0;
                int slot2_typeA_Match = 0;
                int slot2_typeB_Match = 0;
                int slot3_typeA_Match = 0;
                int slot3_typeB_Match = 0;
                int slot4_typeA_Match = 0;
                int slot4_typeB_Match = 0;
                int slot5_typeA_Match = 0;
                int slot5_typeB_Match = 0;
                int slot6_typeA_Match = 0;
                int slot6_typeB_Match = 0;
                foreach (var mon in temp)
                {
                    //check slot1
                    if (mon.acceptableAlts.Contains(pokedex[cb_Slot1.SelectedIndex].dex) && gb_Slot1.Background != Brushes.Green)
                    {//found a match
                        cb_Slot1.SelectedItem = cb_Slot1.Items.IndexOf(mon.name);
                        lbl_Name_Slot1.Content = mon.name;
                        lbl_TypeA_Slot1.Content = mon.type_a;
                        lbl_TypeB_Slot1.Content = mon.type_b;
                        gb_Slot1.Background = Brushes.Green;
                        continue;
                    }
                    //check slot2
                    if (mon.acceptableAlts.Contains(pokedex[cb_Slot2.SelectedIndex].dex) && gb_Slot2.Background != Brushes.Green)
                    {//found a match
                        cb_Slot2.SelectedIndex = cb_Slot2.Items.IndexOf(mon.name);
                        lbl_Name_Slot2.Content = mon.name;
                        lbl_TypeA_Slot2.Content = mon.type_a;
                        lbl_TypeB_Slot2.Content = mon.type_b;
                        gb_Slot2.Background = Brushes.Green;
                        continue;
                    }
                    //check slot3
                    if (mon.acceptableAlts.Contains(pokedex[cb_Slot3.SelectedIndex].dex) && gb_Slot3.Background != Brushes.Green)
                    {//found a match
                        cb_Slot3.SelectedIndex = cb_Slot3.Items.IndexOf(mon.name);
                        lbl_Name_Slot3.Content = mon.name;
                        lbl_TypeA_Slot3.Content = mon.type_a;
                        lbl_TypeB_Slot3.Content = mon.type_b;
                        gb_Slot3.Background = Brushes.Green;
                        continue;
                    }
                    //check slot4
                    if (mon.acceptableAlts.Contains(pokedex[cb_Slot4.SelectedIndex].dex) && gb_Slot4.Background != Brushes.Green)
                    {//found a match
                        cb_Slot4.SelectedIndex = cb_Slot4.Items.IndexOf(mon.name);
                        lbl_Name_Slot4.Content = mon.name;
                        lbl_TypeA_Slot4.Content = mon.type_a;
                        lbl_TypeB_Slot4.Content = mon.type_b;
                        gb_Slot4.Background = Brushes.Green;
                        continue;
                    }
                    //check slot5
                    if (mon.acceptableAlts.Contains(pokedex[cb_Slot5.SelectedIndex].dex) && gb_Slot5.Background != Brushes.Green)
                    {//found a match
                        cb_Slot5.SelectedIndex = cb_Slot5.Items.IndexOf(mon.name);
                        lbl_Name_Slot5.Content = mon.name;
                        lbl_TypeA_Slot5.Content = mon.type_a;
                        lbl_TypeB_Slot5.Content = mon.type_b;
                        gb_Slot5.Background = Brushes.Green;
                        continue;
                    }
                    //check slot6
                    if (mon.acceptableAlts.Contains(pokedex[cb_Slot6.SelectedIndex].dex) && gb_Slot6.Background != Brushes.Green)
                    {//found a match
                        cb_Slot6.SelectedIndex = cb_Slot6.Items.IndexOf(mon.name);
                        lbl_Name_Slot6.Content = mon.name;
                        lbl_TypeA_Slot6.Content = mon.type_a;
                        lbl_TypeB_Slot6.Content = mon.type_b;
                        gb_Slot6.Background = Brushes.Green;
                        continue;
                    }
                }

                if (gb_Slot1.Background == Brushes.Green)
                {
                    temp.Remove(pokedex[cb_Slot1.SelectedIndex]);
                }
                if (gb_Slot2.Background == Brushes.Green)
                {
                    temp.Remove(pokedex[cb_Slot2.SelectedIndex]);
                }
                if (gb_Slot3.Background == Brushes.Green)
                {
                    temp.Remove(pokedex[cb_Slot3.SelectedIndex]);
                }
                if (gb_Slot4.Background == Brushes.Green)
                {
                    temp.Remove(pokedex[cb_Slot4.SelectedIndex]);
                }
                if (gb_Slot5.Background == Brushes.Green)
                {
                    temp.Remove(pokedex[cb_Slot5.SelectedIndex]);
                }
                if (gb_Slot6.Background == Brushes.Green)
                {
                    temp.Remove(pokedex[cb_Slot6.SelectedIndex]);
                }

                foreach (var mon in temp)
                {

                    if (gb_Slot1.Background != Brushes.Green)
                    {//count a match
                        if (pokedex[cb_Slot1.SelectedIndex].type_a == mon.type_a || pokedex[cb_Slot1.SelectedIndex].type_a == mon.type_b)
                        {
                            slot1_typeA_Match++;
                        }
                        if (pokedex[cb_Slot1.SelectedIndex].type_b == mon.type_a || pokedex[cb_Slot1.SelectedIndex].type_b == mon.type_b)
                        {
                            slot1_typeB_Match++;
                        }
                    }

                    if (gb_Slot2.Background != Brushes.Green)
                    {//count a match
                        if (pokedex[cb_Slot2.SelectedIndex].type_a == mon.type_a || pokedex[cb_Slot2.SelectedIndex].type_a == mon.type_b)
                        {
                            slot2_typeA_Match++;
                        }
                        if (pokedex[cb_Slot2.SelectedIndex].type_b == mon.type_a || pokedex[cb_Slot2.SelectedIndex].type_b == mon.type_b)
                        {
                            slot2_typeB_Match++;
                        }
                    }

                    if (gb_Slot3.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_a == pokedex[cb_Slot3.SelectedIndex].type_a || mon.type_b == pokedex[cb_Slot3.SelectedIndex].type_a)
                        {
                            slot3_typeA_Match++;
                        }
                        if (mon.type_a == pokedex[cb_Slot3.SelectedIndex].type_b || mon.type_b == pokedex[cb_Slot3.SelectedIndex].type_b)
                        {
                            slot3_typeB_Match++;
                        }
                    }

                    if (gb_Slot4.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_a == pokedex[cb_Slot4.SelectedIndex].type_a || mon.type_b == pokedex[cb_Slot4.SelectedIndex].type_a)
                        {
                            slot4_typeA_Match++;
                        }
                        if (mon.type_a == pokedex[cb_Slot4.SelectedIndex].type_b || mon.type_b == pokedex[cb_Slot4.SelectedIndex].type_b)
                        {
                            slot4_typeB_Match++;
                        }
                    }

                    if (gb_Slot5.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_a == pokedex[cb_Slot5.SelectedIndex].type_a || mon.type_b == pokedex[cb_Slot5.SelectedIndex].type_a)
                        {
                            slot5_typeA_Match++;
                        }
                        if (mon.type_a == pokedex[cb_Slot5.SelectedIndex].type_b || mon.type_b == pokedex[cb_Slot5.SelectedIndex].type_b)
                        {
                            slot5_typeB_Match++;
                        }
                    }

                    if (gb_Slot6.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_a == pokedex[cb_Slot6.SelectedIndex].type_a || mon.type_b == pokedex[cb_Slot6.SelectedIndex].type_a)
                        {
                            slot6_typeA_Match++;
                        }
                        if (mon.type_a == pokedex[cb_Slot6.SelectedIndex].type_b || mon.type_b == pokedex[cb_Slot6.SelectedIndex].type_b)
                        {
                            slot6_typeB_Match++;
                        }
                    }
                }

                //report counted matches if no perfect match found
                if (slot1_typeA_Match == 0 && slot1_typeB_Match == 0 && gb_Slot1.Background != Brushes.Green)
                {
                    gb_Slot1.Background = Brushes.Red;
                }
                else if (gb_Slot1.Background != Brushes.Green)
                {
                    gb_Slot1.Background = Brushes.DarkOrange;
                }

                if (slot2_typeA_Match == 0 && slot2_typeB_Match == 0 && gb_Slot2.Background != Brushes.Green)
                {
                    gb_Slot2.Background = Brushes.Red;
                }
                else if (gb_Slot2.Background != Brushes.Green)
                {
                    gb_Slot2.Background = Brushes.DarkOrange;
                }
                /////////
                if (slot3_typeA_Match == 0 && slot3_typeB_Match == 0 && gb_Slot3.Background != Brushes.Green)
                {
                    gb_Slot3.Background = Brushes.Red;
                }
                else if (gb_Slot3.Background != Brushes.Green)
                {
                    gb_Slot3.Background = Brushes.DarkOrange;
                }

                if (slot4_typeA_Match == 0 && slot4_typeB_Match == 0 && gb_Slot4.Background != Brushes.Green)
                {
                    gb_Slot4.Background = Brushes.Red;
                }
                else if (gb_Slot4.Background != Brushes.Green)
                {
                    gb_Slot4.Background = Brushes.DarkOrange;
                }
                /////////
                if (slot5_typeA_Match == 0 && slot5_typeB_Match == 0 && gb_Slot5.Background != Brushes.Green)
                {
                    gb_Slot5.Background = Brushes.Red;
                }
                else if (gb_Slot5.Background != Brushes.Green)
                {
                    gb_Slot5.Background = Brushes.DarkOrange;
                }

                if (slot6_typeA_Match == 0 && slot6_typeB_Match == 0 && gb_Slot6.Background != Brushes.Green)
                {
                    gb_Slot6.Background = Brushes.Red;
                }
                else if (gb_Slot6.Background != Brushes.Green)
                {
                    gb_Slot6.Background = Brushes.DarkOrange;
                }
                /////////

                if (gb_Slot1.Background != Brushes.Green)
                {
                    lbl_Name_Slot1.Content = pokedex[cb_Slot1.SelectedIndex].name;
                    lbl_TypeA_Slot1.Content = pokedex[cb_Slot1.SelectedIndex].type_a + " - " + slot1_typeA_Match + " matches";
                    if (pokedex[cb_Slot1.SelectedIndex].type_b != "")
                        lbl_TypeB_Slot1.Content = pokedex[cb_Slot1.SelectedIndex].type_b + " - " + slot1_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot1.Content = "Single type - " + slot1_typeB_Match + " matches";

                }
                if (gb_Slot2.Background != Brushes.Green)
                {
                    lbl_Name_Slot2.Content = pokedex[cb_Slot2.SelectedIndex].name;
                    lbl_TypeA_Slot2.Content = pokedex[cb_Slot2.SelectedIndex].type_a + " - " + slot2_typeA_Match + " matches";
                    if (pokedex[cb_Slot2.SelectedIndex].type_b != "")
                        lbl_TypeB_Slot2.Content = pokedex[cb_Slot2.SelectedIndex].type_b + " - " + slot2_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot2.Content = "Single type - " + slot2_typeB_Match + " matches";
                }
                if (gb_Slot3.Background != Brushes.Green)
                {
                    lbl_Name_Slot3.Content = pokedex[cb_Slot3.SelectedIndex].name;
                    lbl_TypeA_Slot3.Content = pokedex[cb_Slot3.SelectedIndex].type_a + " - " + slot3_typeA_Match + " matches";
                    if (pokedex[cb_Slot3.SelectedIndex].type_b != "")
                        lbl_TypeB_Slot3.Content = pokedex[cb_Slot3.SelectedIndex].type_b + " - " + slot3_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot3.Content = "Single type - " + slot3_typeB_Match + " matches";
                }
                if (gb_Slot4.Background != Brushes.Green)
                {
                    lbl_Name_Slot4.Content = pokedex[cb_Slot4.SelectedIndex].name;
                    lbl_TypeA_Slot4.Content = pokedex[cb_Slot4.SelectedIndex].type_a + " - " + slot4_typeA_Match + " matches";
                    if (pokedex[cb_Slot4.SelectedIndex].type_b != "")
                        lbl_TypeB_Slot4.Content = pokedex[cb_Slot4.SelectedIndex].type_b + " - " + slot4_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot4.Content = "Single type - " + slot4_typeB_Match + " matches";
                }
                if (gb_Slot5.Background != Brushes.Green)
                {
                    lbl_Name_Slot5.Content = pokedex[cb_Slot5.SelectedIndex].name;
                    lbl_TypeA_Slot5.Content = pokedex[cb_Slot5.SelectedIndex].type_a + " - " + slot5_typeA_Match + " matches";
                    if (pokedex[cb_Slot5.SelectedIndex].type_b != "")
                        lbl_TypeB_Slot5.Content = pokedex[cb_Slot5.SelectedIndex].type_b + " - " + slot5_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot5.Content = "Single type - " + slot5_typeB_Match + " matches";
                }
                if (gb_Slot6.Background != Brushes.Green)
                {
                    lbl_Name_Slot6.Content = pokedex[cb_Slot6.SelectedIndex].name;
                    lbl_TypeA_Slot6.Content = pokedex[cb_Slot6.SelectedIndex].type_a + " - " + slot6_typeA_Match + " matches";
                    if (pokedex[cb_Slot6.SelectedIndex].type_b != "")
                        lbl_TypeB_Slot6.Content = pokedex[cb_Slot6.SelectedIndex].type_b + " - " + slot6_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot6.Content = "Single type - " + slot6_typeB_Match + " matches";
                }
            }

        }
    }

    public class Pokemon
    {
        [XmlAttribute]
        public int dex { get; set; }

        [XmlAttribute]
        public string name { get; set; }

        [XmlAttribute]
        public string type_a { get; set; }

        [XmlAttribute]
        public string type_b { get; set; }

        [XmlAttribute]
        public string ability { get; set; }

        [XmlAttribute]
        public string secondAbility { get; set; }

        [XmlAttribute]
        public string hiddenAbility { get; set; }
        [XmlAttribute]
        public bool legendary { get; set; }

        [XmlAttribute]
        public List<int> acceptableAlts { get; set; }
        [XmlAttribute]
        public int generation { get; set; }
        [XmlAttribute]
        public string originRegion { get; set; }
        [XmlAttribute]
        public List<int> evolutionaryFamily { get; set; }
        [XmlAttribute]
        public string evolutionMethod;
        public Pokemon()
        {

        }
        public Pokemon(int d, string species, string firstType, string secondType, string firstAbil, string secondAbil, string hiddenAbil, bool legend, List<int> altDexValues, int gen, string region, List<int> family, string evoMethod)
        {
            dex = d;
            name = species;
            type_a = firstType;
            type_b = secondType;
            ability = firstAbil;
            secondAbility = secondAbil;
            hiddenAbility = hiddenAbil;
            legendary = legend;
            acceptableAlts = altDexValues;
            generation = gen;
            originRegion = region;
            evolutionaryFamily = family;
            evolutionMethod = evoMethod;
        }
    }
}