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
                    Pokemon mon = new Pokemon();
                    mon.dex_number = Convert.ToInt32(fields[0]);
                    mon.name = fields[1];
                    mon.type_01 = fields[2];
                    mon.type_02 = fields[3];
                    mon.region = fields[4];
                    mon.generation = Convert.ToInt32(fields[5]);
                    mon.evo_method = fields[6];
                    mon.evo_family = fields[7].Split(';').Select(Int32.Parse).ToList();
                    mon.is_legendry = Convert.ToBoolean(fields[8]);
                    mon.is_mythical = Convert.ToBoolean(fields[9]);

                    Dictionary<string, string> gameFieldParser = fields[10 + cb_game.SelectedIndex].Split('~').Select(part => part.Split('=')).Where(part => part.Length == 2).ToDictionary(sp => sp[0], sp => sp[1]);

                    if (gameFieldParser.ContainsKey("Available"))
                    {
                        if (Convert.ToBoolean(gameFieldParser["Available"]) == false)
                        {
                            continue;
                        }
                    }
                    if (gameFieldParser.ContainsKey("Override")) 
                    {
                        Dictionary<string, string> overrideParser = gameFieldParser["Override"].Split('&').Select(part => part.Split('-')).Where(part => part.Length == 2).ToDictionary(sp => sp[0], sp => sp[1]);

                        if (overrideParser.ContainsKey("dex_number"))
                        {
                            mon.dex_number = Convert.ToInt32(overrideParser["dex_number"]);
                        }
                        if (overrideParser.ContainsKey("name"))
                        {
                            mon.name = overrideParser["name"];
                        }
                        if (overrideParser.ContainsKey("type_01"))
                        {
                            mon.type_01 = overrideParser["type_01"];
                        }
                        if (overrideParser.ContainsKey("type_02"))
                        {
                            if (overrideParser["type_02"] == "none")
                            {
                                mon.type_02 = "";
                            }
                            else
                            {
                                mon.type_02 = overrideParser["type_02"];
                            }
                        }
                        if (overrideParser.ContainsKey("region"))
                        {
                            mon.region = overrideParser["region"];
                        }
                        if (overrideParser.ContainsKey("generation"))
                        {
                            mon.generation = Convert.ToInt32(overrideParser["generation"]);
                        }
                        if (overrideParser.ContainsKey("evo_method"))
                        {
                            mon.evo_method = overrideParser["evo_method"];
                        }
                        if (overrideParser.ContainsKey("evo_family"))
                        {
                            mon.evo_family = overrideParser["evo_family"].Split(';').Select(Int32.Parse).ToList();
                        }
                        if (overrideParser.ContainsKey("is_legendry"))
                        {
                            mon.is_legendry = Convert.ToBoolean(overrideParser["is_legendry"]);
                        }
                        if (overrideParser.ContainsKey("is_mythical"))
                        {
                            mon.is_mythical = Convert.ToBoolean(overrideParser["is_mythical"]);
                        }
                    }
                    if (gameFieldParser.ContainsKey("Alt Forms"))
                    {

                    }
                    if (gameFieldParser.ContainsKey("Mutually Exclusive"))
                    {
                        mon.mutuallyExclusiveDexValues = gameFieldParser["Mutually Exclusive"].Split(';').Select(Int32.Parse).ToList();
                    }
                    else
                    {
                        List<int> list = new List<int>();
                        list.Add(mon.dex_number);
                        mon.mutuallyExclusiveDexValues = list;
                    }

                    mon.game = cb_game.SelectedIndex;
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
                while (randoParty.Count != 0 && (randoParty.Contains(randoMon) || randoParty.All(x => x.mutuallyExclusiveDexValues.Contains(randoMon.dex_number)) || (randoMon.is_legendry && ck_legendries.IsChecked == false)))
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
                    if (mon.mutuallyExclusiveDexValues.Contains(pokedex[cb_Slot1.SelectedIndex].dex_number) && gb_Slot1.Background != Brushes.Green)
                    {//found a match
                        cb_Slot1.SelectedItem = cb_Slot1.Items.IndexOf(mon.name);
                        lbl_Name_Slot1.Content = mon.name;
                        lbl_TypeA_Slot1.Content = mon.type_01;
                        lbl_TypeB_Slot1.Content = mon.type_02;
                        gb_Slot1.Background = Brushes.Green;
                        continue;
                    }
                    //check slot2
                    if (mon.mutuallyExclusiveDexValues.Contains(pokedex[cb_Slot2.SelectedIndex].dex_number) && gb_Slot2.Background != Brushes.Green)
                    {//found a match
                        cb_Slot2.SelectedIndex = cb_Slot2.Items.IndexOf(mon.name);
                        lbl_Name_Slot2.Content = mon.name;
                        lbl_TypeA_Slot2.Content = mon.type_01;
                        lbl_TypeB_Slot2.Content = mon.type_02;
                        gb_Slot2.Background = Brushes.Green;
                        continue;
                    }
                    //check slot3
                    if (mon.mutuallyExclusiveDexValues.Contains(pokedex[cb_Slot3.SelectedIndex].dex_number) && gb_Slot3.Background != Brushes.Green)
                    {//found a match
                        cb_Slot3.SelectedIndex = cb_Slot3.Items.IndexOf(mon.name);
                        lbl_Name_Slot3.Content = mon.name;
                        lbl_TypeA_Slot3.Content = mon.type_01;
                        lbl_TypeB_Slot3.Content = mon.type_02;
                        gb_Slot3.Background = Brushes.Green;
                        continue;
                    }
                    //check slot4
                    if (mon.mutuallyExclusiveDexValues.Contains(pokedex[cb_Slot4.SelectedIndex].dex_number) && gb_Slot4.Background != Brushes.Green)
                    {//found a match
                        cb_Slot4.SelectedIndex = cb_Slot4.Items.IndexOf(mon.name);
                        lbl_Name_Slot4.Content = mon.name;
                        lbl_TypeA_Slot4.Content = mon.type_01;
                        lbl_TypeB_Slot4.Content = mon.type_02;
                        gb_Slot4.Background = Brushes.Green;
                        continue;
                    }
                    //check slot5
                    if (mon.mutuallyExclusiveDexValues.Contains(pokedex[cb_Slot5.SelectedIndex].dex_number) && gb_Slot5.Background != Brushes.Green)
                    {//found a match
                        cb_Slot5.SelectedIndex = cb_Slot5.Items.IndexOf(mon.name);
                        lbl_Name_Slot5.Content = mon.name;
                        lbl_TypeA_Slot5.Content = mon.type_01;
                        lbl_TypeB_Slot5.Content = mon.type_02;
                        gb_Slot5.Background = Brushes.Green;
                        continue;
                    }
                    //check slot6
                    if (mon.mutuallyExclusiveDexValues.Contains(pokedex[cb_Slot6.SelectedIndex].dex_number) && gb_Slot6.Background != Brushes.Green)
                    {//found a match
                        cb_Slot6.SelectedIndex = cb_Slot6.Items.IndexOf(mon.name);
                        lbl_Name_Slot6.Content = mon.name;
                        lbl_TypeA_Slot6.Content = mon.type_01;
                        lbl_TypeB_Slot6.Content = mon.type_02;
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
                        if (pokedex[cb_Slot1.SelectedIndex].type_01 == mon.type_01 || pokedex[cb_Slot1.SelectedIndex].type_01 == mon.type_02)
                        {
                            slot1_typeA_Match++;
                        }
                        if (pokedex[cb_Slot1.SelectedIndex].type_02 == mon.type_01 || pokedex[cb_Slot1.SelectedIndex].type_02 == mon.type_02)
                        {
                            slot1_typeB_Match++;
                        }
                    }

                    if (gb_Slot2.Background != Brushes.Green)
                    {//count a match
                        if (pokedex[cb_Slot2.SelectedIndex].type_01 == mon.type_01 || pokedex[cb_Slot2.SelectedIndex].type_01 == mon.type_02)
                        {
                            slot2_typeA_Match++;
                        }
                        if (pokedex[cb_Slot2.SelectedIndex].type_02 == mon.type_01 || pokedex[cb_Slot2.SelectedIndex].type_02 == mon.type_02)
                        {
                            slot2_typeB_Match++;
                        }
                    }

                    if (gb_Slot3.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_01 == pokedex[cb_Slot3.SelectedIndex].type_01 || mon.type_02 == pokedex[cb_Slot3.SelectedIndex].type_01)
                        {
                            slot3_typeA_Match++;
                        }
                        if (mon.type_01 == pokedex[cb_Slot3.SelectedIndex].type_02 || mon.type_02 == pokedex[cb_Slot3.SelectedIndex].type_02)
                        {
                            slot3_typeB_Match++;
                        }
                    }

                    if (gb_Slot4.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_01 == pokedex[cb_Slot4.SelectedIndex].type_01 || mon.type_02 == pokedex[cb_Slot4.SelectedIndex].type_01)
                        {
                            slot4_typeA_Match++;
                        }
                        if (mon.type_01 == pokedex[cb_Slot4.SelectedIndex].type_02 || mon.type_02 == pokedex[cb_Slot4.SelectedIndex].type_02)
                        {
                            slot4_typeB_Match++;
                        }
                    }

                    if (gb_Slot5.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_01 == pokedex[cb_Slot5.SelectedIndex].type_01 || mon.type_02 == pokedex[cb_Slot5.SelectedIndex].type_01)
                        {
                            slot5_typeA_Match++;
                        }
                        if (mon.type_01 == pokedex[cb_Slot5.SelectedIndex].type_02 || mon.type_02 == pokedex[cb_Slot5.SelectedIndex].type_02)
                        {
                            slot5_typeB_Match++;
                        }
                    }

                    if (gb_Slot6.Background != Brushes.Green)
                    {//count a match
                        if (mon.type_01 == pokedex[cb_Slot6.SelectedIndex].type_01 || mon.type_02 == pokedex[cb_Slot6.SelectedIndex].type_01)
                        {
                            slot6_typeA_Match++;
                        }
                        if (mon.type_01 == pokedex[cb_Slot6.SelectedIndex].type_02 || mon.type_02 == pokedex[cb_Slot6.SelectedIndex].type_02)
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
                    lbl_TypeA_Slot1.Content = pokedex[cb_Slot1.SelectedIndex].type_01 + " - " + slot1_typeA_Match + " matches";
                    if (pokedex[cb_Slot1.SelectedIndex].type_02 != "")
                        lbl_TypeB_Slot1.Content = pokedex[cb_Slot1.SelectedIndex].type_02 + " - " + slot1_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot1.Content = "Single type - " + slot1_typeB_Match + " matches";

                }
                if (gb_Slot2.Background != Brushes.Green)
                {
                    lbl_Name_Slot2.Content = pokedex[cb_Slot2.SelectedIndex].name;
                    lbl_TypeA_Slot2.Content = pokedex[cb_Slot2.SelectedIndex].type_01 + " - " + slot2_typeA_Match + " matches";
                    if (pokedex[cb_Slot2.SelectedIndex].type_02 != "")
                        lbl_TypeB_Slot2.Content = pokedex[cb_Slot2.SelectedIndex].type_02 + " - " + slot2_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot2.Content = "Single type - " + slot2_typeB_Match + " matches";
                }
                if (gb_Slot3.Background != Brushes.Green)
                {
                    lbl_Name_Slot3.Content = pokedex[cb_Slot3.SelectedIndex].name;
                    lbl_TypeA_Slot3.Content = pokedex[cb_Slot3.SelectedIndex].type_01 + " - " + slot3_typeA_Match + " matches";
                    if (pokedex[cb_Slot3.SelectedIndex].type_02 != "")
                        lbl_TypeB_Slot3.Content = pokedex[cb_Slot3.SelectedIndex].type_02 + " - " + slot3_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot3.Content = "Single type - " + slot3_typeB_Match + " matches";
                }
                if (gb_Slot4.Background != Brushes.Green)
                {
                    lbl_Name_Slot4.Content = pokedex[cb_Slot4.SelectedIndex].name;
                    lbl_TypeA_Slot4.Content = pokedex[cb_Slot4.SelectedIndex].type_01 + " - " + slot4_typeA_Match + " matches";
                    if (pokedex[cb_Slot4.SelectedIndex].type_02 != "")
                        lbl_TypeB_Slot4.Content = pokedex[cb_Slot4.SelectedIndex].type_02 + " - " + slot4_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot4.Content = "Single type - " + slot4_typeB_Match + " matches";
                }
                if (gb_Slot5.Background != Brushes.Green)
                {
                    lbl_Name_Slot5.Content = pokedex[cb_Slot5.SelectedIndex].name;
                    lbl_TypeA_Slot5.Content = pokedex[cb_Slot5.SelectedIndex].type_01 + " - " + slot5_typeA_Match + " matches";
                    if (pokedex[cb_Slot5.SelectedIndex].type_02 != "")
                        lbl_TypeB_Slot5.Content = pokedex[cb_Slot5.SelectedIndex].type_02 + " - " + slot5_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot5.Content = "Single type - " + slot5_typeB_Match + " matches";
                }
                if (gb_Slot6.Background != Brushes.Green)
                {
                    lbl_Name_Slot6.Content = pokedex[cb_Slot6.SelectedIndex].name;
                    lbl_TypeA_Slot6.Content = pokedex[cb_Slot6.SelectedIndex].type_01 + " - " + slot6_typeA_Match + " matches";
                    if (pokedex[cb_Slot6.SelectedIndex].type_02 != "")
                        lbl_TypeB_Slot6.Content = pokedex[cb_Slot6.SelectedIndex].type_02 + " - " + slot6_typeB_Match + " matches";
                    else
                        lbl_TypeB_Slot6.Content = "Single type - " + slot6_typeB_Match + " matches";
                }
            }

        }
    }

    public class Pokemon
    {
        [XmlAttribute]
        public int dex_number { get; set; }

        [XmlAttribute]
        public string name { get; set; }

        [XmlAttribute]
        public string type_01 { get; set; }
        [XmlAttribute]
        public string type_02 { get; set; }
        [XmlAttribute] 
        public string region { get; set; }
        [XmlAttribute]
        public int generation {  get; set; }
        [XmlAttribute]
        public string evo_method { get; set; }
        [XmlAttribute]
        public List<int> evo_family { get; set; }
        [XmlAttribute]
        public bool is_legendry { get; set; }
        [XmlAttribute]
        public bool is_mythical { get; set; }
        [XmlAttribute]
        public List<int> mutuallyExclusiveDexValues { get; set; }
        [XmlAttribute]
        public int game {  get; set; }
        public Pokemon()
        {
            this.dex_number = -1;
            this.name = "";
            this.type_01 = "";
            this.type_02 = "";
            this.region = "";
            this.generation = -1;
            this.evo_method = "";
            this.evo_family = new List<int>();
            this.is_legendry = false;
            this.is_mythical = false;
            this.mutuallyExclusiveDexValues = new List<int>();
            this.game = -1;

        }
        public Pokemon(int dex_number, string name, string type_01, string type_02, string region, int generation, string evo_method, List<int> evo_family, bool is_legendry, bool is_mythical, List<int> mutuallyExclusiveDexValues, int game)
        {
            this.dex_number = dex_number;
            this.name = name;
            this.type_01 = type_01;
            this.type_02 = type_02;
            this.region = region;
            this.generation = generation;
            this.evo_method = evo_method;
            this.evo_family = evo_family;
            this.is_legendry = is_legendry;
            this.is_mythical = is_mythical;
            this.mutuallyExclusiveDexValues = mutuallyExclusiveDexValues;
            this.game = game;
        }
    }
}