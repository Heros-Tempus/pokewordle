using Microsoft.VisualBasic.FileIO;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
            cb_Slot1.ItemsSource = pokedex;
            cb_Slot2.ItemsSource = pokedex;
            cb_Slot3.ItemsSource = pokedex;
            cb_Slot4.ItemsSource = pokedex;
            cb_Slot5.ItemsSource = pokedex;
            cb_Slot6.ItemsSource = pokedex;
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
                        setVisablePartySlots(secretParty.Count);
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
                    setVisablePartySlots(secretParty.Count);
                    Save(secretParty);
                }
            }
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            saveFilePath = Microsoft.VisualBasic.FileSystem.CurDir() + "\\Resources\\savefile";
            secretParty.Clear();
            try
            {
                secretParty = Load();
                setVisablePartySlots(secretParty.Count);
                sl_party_size.Value = secretParty.Count;
                cb_game.SelectedIndex = secretParty[0].game;
            }
            catch (Exception)
            {
                //couldnt load party
                File.WriteAllText(saveFilePath, null);
                cb_game.SelectedIndex = 0;
            }
            generateDex();
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
            outFile.Close();
        }
        private void generateDex()
        {
            pokedex.Clear();
            using (TextFieldParser parser = new TextFieldParser(Microsoft.VisualBasic.FileSystem.CurDir() + "\\Resources\\pokemon_dataset.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                string[] header = parser.ReadFields();
                string[] regions = header.Skip(13).ToArray();
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
                    mon.baby = Convert.ToBoolean(fields[10]);
                    mon.final_evo = Convert.ToBoolean(fields[11]);

                    Dictionary<string, string> gameFieldParser = fields[13 + cb_game.SelectedIndex].Split('~').Select(part => part.Split('=')).Where(part => part.Length == 2).ToDictionary(sp => sp[0], sp => sp[1]);

                    if (gameFieldParser.ContainsKey("Available"))
                    {
                        if (Convert.ToBoolean(gameFieldParser["Available"]) == false)
                        {
                            continue;
                        }
                    }
                    if (gameFieldParser.ContainsKey("Catchable"))
                    {
                        mon.ignore_evo_restriction = Convert.ToBoolean(gameFieldParser["Catchable"]);
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
                    if (gameFieldParser.ContainsKey("Mutually Exclusive"))
                    {
                        mon.mutuallyExclusiveDexValues = gameFieldParser["Mutually Exclusive"].Split(';').Select(Int32.Parse).ToList();
                    }
                    else
                    {
                        List<int> list = new List<int>();
                        list.Add(-1);
                        mon.mutuallyExclusiveDexValues = list;
                    }

                    mon.game = cb_game.SelectedIndex;

                    pokedex.Add(mon);
                    if (gameFieldParser.ContainsKey("Alt Forms"))
                    {
                        mon.form_name = fields[12];

                        List<string> forms = gameFieldParser["Alt Forms"].Split('|').ToList();

                        foreach (string form in forms)
                        {
                            Pokemon altMon = new Pokemon();
                            var formParser = form.Split('&').Select(part => part.Split('-')).Where(part => part.Length == 2).ToDictionary(sp => sp[0], sp => sp[1]);
                            altMon.dex_number = mon.dex_number;
                            altMon.name = mon.name;
                            altMon.form_name = formParser["form_name"];
                            altMon.type_01 = formParser["type_01"];
                            altMon.type_02 = formParser["type_02"];
                            altMon.region = formParser["region"];
                            altMon.generation = Convert.ToInt32(formParser["generation"]);
                            altMon.evo_method = formParser["evo_method"];
                            altMon.evo_family = formParser["evo_family"].Split(';').Select(Int32.Parse).ToList();
                            altMon.is_legendry = Convert.ToBoolean(formParser["is_legendry"]);
                            altMon.is_mythical = Convert.ToBoolean(formParser["is_mythical"]);
                            altMon.baby = Convert.ToBoolean(formParser["is_baby"]);
                            altMon.final_evo = Convert.ToBoolean(formParser["is_final"]);
                            pokedex.Add(altMon);
                        }
                    }
                }
                cb_Slot1.Items.Refresh();
                cb_Slot2.Items.Refresh();
                cb_Slot3.Items.Refresh();
                cb_Slot4.Items.Refresh();
                cb_Slot5.Items.Refresh();
                cb_Slot6.Items.Refresh();
            }
        }
    
        private List<Pokemon> generateParty()
        {
            //reset slot 1
            {
                gb_Slot1.Background = Brushes.Transparent;
                foreach (object o in slot1.Children)
                {
                    string? name = null;
                    if (o is Label)
                    {
                        name = (o as Label).Name;
                    }
                    switch (name)
                    {
                        case "lbl_Name_Slot1":
                            (o as Label).Content = "Name:";
                            break;
                        case "lbl_Type01_Slot1":
                            (o as Label).Content = "TypeA:";
                            break;
                        case "lbl_Type02_Slot1":
                            (o as Label).Content = "TypeB:";
                            break;
                        case "lbl_Region_Slot1":
                            (o as Label).Content = "Region:";
                            break;
                        case "lbl_Gen_Slot1":
                            (o as Label).Content = "Generation:";
                            break;
                        case "lbl_EMethod_Slot1":
                            (o as Label).Content = "Evo Method:";
                            break;
                        case "lbl_Family_Slot1":
                            (o as Label).Content = "";
                            break;
                    }
                }
            }
            //reset slot 2
            {
                gb_Slot2.Background = Brushes.Transparent;
                foreach (object o in slot2.Children)
                {
                    string? name = null;
                    if (o is Label)
                    {
                        name = (o as Label).Name;
                    }
                    switch (name)
                    {
                        case "lbl_Name_Slot2":
                            (o as Label).Content = "Name:";
                            break;
                        case "lbl_Type01_Slot2":
                            (o as Label).Content = "TypeA:";
                            break;
                        case "lbl_Type02_Slot2":
                            (o as Label).Content = "TypeB:";
                            break;
                        case "lbl_Region_Slot2":
                            (o as Label).Content = "Region:";
                            break;
                        case "lbl_Gen_Slot2":
                            (o as Label).Content = "Generation:";
                            break;
                        case "lbl_EMethod_Slot2":
                            (o as Label).Content = "Evo Method:";
                            break;
                        case "lbl_Family_Slot2":
                            (o as Label).Content = "";
                            break;
                    }
                }
            }
            //reset slot 3
            {
                gb_Slot3.Background = Brushes.Transparent;
                foreach (object o in slot3.Children)
                {
                    string? name = null;
                    if (o is Label)
                    {
                        name = (o as Label).Name;
                    }
                    switch (name)
                    {
                        case "lbl_Name_Slot3":
                            (o as Label).Content = "Name:";
                            break;
                        case "lbl_Type01_Slot3":
                            (o as Label).Content = "TypeA:";
                            break;
                        case "lbl_Type02_Slot3":
                            (o as Label).Content = "TypeB:";
                            break;
                        case "lbl_Region_Slot3":
                            (o as Label).Content = "Region:";
                            break;
                        case "lbl_Gen_Slot3":
                            (o as Label).Content = "Generation:";
                            break;
                        case "lbl_EMethod_Slot3":
                            (o as Label).Content = "Evo Method:";
                            break;
                        case "lbl_Family_Slot3":
                            (o as Label).Content = "";
                            break;
                    }
                }
            }
            //reset slot 4
            {
                gb_Slot4.Background = Brushes.Transparent;
                foreach (object o in slot4.Children)
                {
                    string? name = null;
                    if (o is Label)
                    {
                        name = (o as Label).Name;
                    }
                    switch (name)
                    {
                        case "lbl_Name_Slot4":
                            (o as Label).Content = "Name:";
                            break;
                        case "lbl_Type01_Slot4":
                            (o as Label).Content = "TypeA:";
                            break;
                        case "lbl_Type02_Slot4":
                            (o as Label).Content = "TypeB:";
                            break;
                        case "lbl_Region_Slot4":
                            (o as Label).Content = "Region:";
                            break;
                        case "lbl_Gen_Slot4":
                            (o as Label).Content = "Generation:";
                            break;
                        case "lbl_EMethod_Slot4":
                            (o as Label).Content = "Evo Method:";
                            break;
                        case "lbl_Family_Slot4":
                            (o as Label).Content = "";
                            break;
                    }
                }
            }
            //reset slot 5
            {
                gb_Slot5.Background = Brushes.Transparent;
                foreach (object o in slot5.Children)
                {
                    string? name = null;
                    if (o is Label)
                    {
                        name = (o as Label).Name;
                    }
                    switch (name)
                    {
                        case "lbl_Name_Slot5":
                            (o as Label).Content = "Name:";
                            break;
                        case "lbl_Type01_Slot5":
                            (o as Label).Content = "TypeA:";
                            break;
                        case "lbl_Type02_Slot5":
                            (o as Label).Content = "TypeB:";
                            break;
                        case "lbl_Region_Slot5":
                            (o as Label).Content = "Region:";
                            break;
                        case "lbl_Gen_Slot5":
                            (o as Label).Content = "Generation:";
                            break;
                        case "lbl_EMethod_Slot5":
                            (o as Label).Content = "Evo Method:";
                            break;
                        case "lbl_Family_Slot5":
                            (o as Label).Content = "";
                            break;
                    }
                }
            }
            //reset slot 6
            {
                gb_Slot6.Background = Brushes.Transparent;
                foreach (object o in slot6.Children)
                {
                    string? name = null;
                    if (o is Label)
                    {
                        name = (o as Label).Name;
                    }
                    switch (name)
                    {
                        case "lbl_Name_Slot6":
                            (o as Label).Content = "Name:";
                            break;
                        case "lbl_Type01_Slot6":
                            (o as Label).Content = "TypeA:";
                            break;
                        case "lbl_Type02_Slot6":
                            (o as Label).Content = "TypeB:";
                            break;
                        case "lbl_Region_Slot6":
                            (o as Label).Content = "Region:";
                            break;
                        case "lbl_Gen_Slot6":
                            (o as Label).Content = "Generation:";
                            break;
                        case "lbl_EMethod_Slot6":
                            (o as Label).Content = "Evo Method:";
                            break;
                        case "lbl_Family_Slot6":
                            (o as Label).Content = "";
                            break;
                    }
                }
            }

            List<Pokemon> randoParty = new List<Pokemon>();

            do
            {
                var random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);
                int index = random.Next(pokedex.Count());
                Pokemon randoMon = pokedex[index];

                if (randoMon.is_legendry && ck_allow_legendries.IsChecked == false) { continue; }

                if (randoMon.is_mythical && ck_allow_mythicals.IsChecked == false) {  continue; }

                if (randoMon.evo_method == "Trade" && ck_allow_trade_evos.IsChecked == false && !randoMon.ignore_evo_restriction) { continue; }

                if (randoParty.Any(x => x.mutuallyExclusiveDexValues.Contains(randoMon.dex_number)) && ck_allow_exclusives.IsChecked == false) { continue; }

                if (randoMon.baby && ck_allow_babies.IsChecked == false) { continue; }

                if (!randoMon.final_evo && ck_final_evo_only.IsChecked == true) { continue; }

                if ((randoMon.evo_method == "Used item" || randoMon.evo_method == "Held item") && ck_disable_item_evos.IsChecked == true && !randoMon.ignore_evo_restriction) { continue; }

                if (randoMon.evo_method == "Friendship" && ck_disable_friendship_evos.IsChecked == true && !randoMon.ignore_evo_restriction) { continue; }

                if (randoMon.evo_method == "Unique" && ck_disable_unique_evos.IsChecked == true && !randoMon.ignore_evo_restriction) { continue; }

                randoParty.Add(randoMon);

                if (ck_allow_dupes.IsChecked == false)
                {
                    randoParty = randoParty.Select(x=> x).Distinct().ToList();
                }
            } while (randoParty.Count < sl_party_size.Value);
            return randoParty;
        }
        private void btn_CheckParty_Click(object sender, RoutedEventArgs e)
        {
            if (cb_Slot1.SelectedIndex == -1 || (cb_Slot2.SelectedIndex == -1 && secretParty.Count >= 2) || (cb_Slot3.SelectedIndex == -1 && secretParty.Count >= 3) || (cb_Slot4.SelectedIndex == -1 && secretParty.Count >= 4) || (cb_Slot5.SelectedIndex == -1 && secretParty.Count >= 5) || (cb_Slot6.SelectedIndex == -1 && secretParty.Count >= 6))
            {
                string gameMessageBoxText = "Please fill out the party before checking.";
                string c = "Check Party";
                MessageBoxButton b = MessageBoxButton.OK;
                MessageBoxImage i = MessageBoxImage.Warning;
                MessageBox.Show(gameMessageBoxText, c, b, i, MessageBoxResult.OK);
            }
            else if (secretParty.Count < 1)
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

                List<string> keys = new List<string>{ "type01", "type02", "region", "gen", "evo_method", "evo_family" };
                var slot1_matches = keys.ToDictionary(k => k, k => 0);
                var slot2_matches = keys.ToDictionary(k => k, k => 0);
                var slot3_matches = keys.ToDictionary(k => k, k => 0);
                var slot4_matches = keys.ToDictionary(k => k, k => 0);
                var slot5_matches = keys.ToDictionary(k => k, k => 0);
                var slot6_matches = keys.ToDictionary(k => k, k => 0);

                var slot1 = cb_Slot1.SelectedItem as Pokemon;
                var slot2 = cb_Slot1.SelectedItem as Pokemon;
                var slot3 = cb_Slot1.SelectedItem as Pokemon;
                var slot4 = cb_Slot1.SelectedItem as Pokemon;
                var slot5 = cb_Slot1.SelectedItem as Pokemon;
                var slot6 = cb_Slot1.SelectedItem as Pokemon;
                //check for direct matches
                foreach (var mon in temp)
                {
                    //check slot1
                    if (((mon.name == slot1.name && mon.form_name == slot1.form_name) || mon.mutuallyExclusiveDexValues.Contains(slot1.dex_number)) && gb_Slot1.Background != Brushes.Green)
                    {//found a match
                        cb_Slot1.SelectedIndex = cb_Slot1.Items.IndexOf(mon);
                        gb_Slot1.Background = Brushes.Green;
                        continue;
                    }
                    //check slot2
                    if (secretParty.Count >= 2 && ((mon.name == slot2.name && mon.form_name == slot2.form_name) || mon.mutuallyExclusiveDexValues.Contains(slot2.dex_number)) && gb_Slot2.Background != Brushes.Green)
                    {//found a match
                        cb_Slot2.SelectedIndex = cb_Slot2.Items.IndexOf(mon);
                        gb_Slot2.Background = Brushes.Green;
                        continue;
                    }
                    //check slot3
                    if (secretParty.Count >= 3 && ((mon.name == slot3.name && mon.form_name == slot3.form_name) || mon.mutuallyExclusiveDexValues.Contains(slot3.dex_number)) && gb_Slot3.Background != Brushes.Green)
                    {//found a match
                        cb_Slot3.SelectedIndex = cb_Slot3.Items.IndexOf(mon);
                        gb_Slot3.Background = Brushes.Green;
                        continue;
                    }
                    //check slot4
                    if (secretParty.Count >= 4 && ((mon.name == slot4.name && mon.form_name == slot4.form_name) || mon.mutuallyExclusiveDexValues.Contains(slot4.dex_number)) && gb_Slot4.Background != Brushes.Green)
                    {//found a match
                        cb_Slot4.SelectedIndex = cb_Slot4.Items.IndexOf(mon);
                        gb_Slot4.Background = Brushes.Green;
                        continue;
                    }
                    //check slot5
                    if (secretParty.Count >= 5 && ((mon.name == slot5.name && mon.form_name == slot5.form_name) || mon.mutuallyExclusiveDexValues.Contains(slot5.dex_number)) && gb_Slot5.Background != Brushes.Green)
                    {//found a match
                        cb_Slot5.SelectedIndex = cb_Slot5.Items.IndexOf(mon);
                        gb_Slot5.Background = Brushes.Green;
                        continue;
                    }
                    //check slot6
                    if (secretParty.Count >= 6 && ((mon.name == slot6.name && mon.form_name == slot6.form_name) || mon.mutuallyExclusiveDexValues.Contains(slot6.dex_number)) && gb_Slot6.Background != Brushes.Green)
                    {//found a match
                        cb_Slot6.SelectedIndex = cb_Slot6.Items.IndexOf(mon);
                        gb_Slot6.Background = Brushes.Green;
                        continue;
                    }
                }

                ////exclude direct matches from further checks
                //if (gb_Slot1.Background == Brushes.Green)
                //    temp.Remove(temp.Find(x=>x.name== pokedex[cb_Slot1.SelectedIndex].name));
                //if (secretParty.Count >= 2 && gb_Slot2.Background == Brushes.Green)
                //    temp.Remove(temp.Find(x => x.name == pokedex[cb_Slot2.SelectedIndex].name));
                //if (secretParty.Count >= 3 && gb_Slot3.Background == Brushes.Green)
                //    temp.Remove(temp.Find(x => x.name == pokedex[cb_Slot3.SelectedIndex].name));
                //if (secretParty.Count >= 4 && gb_Slot4.Background == Brushes.Green)
                //    temp.Remove(temp.Find(x => x.name == pokedex[cb_Slot4.SelectedIndex].name));
                //if (secretParty.Count >= 5 && gb_Slot5.Background == Brushes.Green)
                //    temp.Remove(temp.Find(x => x.name == pokedex[cb_Slot5.SelectedIndex].name));
                //if (secretParty.Count >= 6 && gb_Slot6.Background == Brushes.Green)
                //    temp.Remove(temp.Find(x => x.name == pokedex[cb_Slot6.SelectedIndex].name));

                //count number of matched properties for any mon that isnt a direct match
                foreach (var mon in temp)
                {
                    if (slot1.type_01 == mon.type_01 || slot1.type_01 == mon.type_02)
                        slot1_matches["type01"]++;
                    if (slot1.type_02 == mon.type_01 || slot1.type_02 == mon.type_02)
                        slot1_matches["type02"]++;
                    if (slot1.region == mon.region)
                        slot1_matches["region"]++;
                    if (slot1.generation == mon.generation)
                        slot1_matches["gen"]++;
                    if (slot1.evo_method == mon.evo_method)
                        slot1_matches["evo_method"]++;
                    if (slot1.evo_family.Contains(mon.dex_number))
                        slot1_matches["evo_family"]++;
                    if (secretParty.Count >= 2)
                    {//count matching properties for slot 2
                        if (slot2.type_01 == mon.type_01 || slot2.type_01 == mon.type_02)
                            slot2_matches["type01"]++;
                        if (slot2.type_02 == mon.type_01 || slot2.type_02 == mon.type_02)
                            slot2_matches["type02"]++;
                        if (slot2.region == mon.region)
                            slot2_matches["region"]++;
                        if (slot2.generation == mon.generation)
                            slot2_matches["gen"]++;
                        if (slot2.evo_method == mon.evo_method)
                            slot2_matches["evo_method"]++;
                        if (slot2.evo_family.Contains(mon.dex_number))
                            slot2_matches["evo_family"]++;
                    }
                    if (secretParty.Count >= 3)
                    {//count matching properties for slot 3
                        if (slot3.type_01 == mon.type_01 || slot3.type_01 == mon.type_02)
                            slot3_matches["type01"]++;
                        if (slot3.type_02 == mon.type_01 || slot3.type_02 == mon.type_02)
                            slot3_matches["type02"]++;
                        if (slot3.region == mon.region)
                            slot3_matches["region"]++;
                        if (slot3.generation == mon.generation)
                            slot3_matches["gen"]++;
                        if (slot3.evo_method == mon.evo_method)
                            slot3_matches["evo_method"]++;
                        if (slot3.evo_family.Contains(mon.dex_number))
                            slot3_matches["evo_family"]++;
                    }
                    if (secretParty.Count >= 4)
                    {//count matching properties for slot 4
                        if (slot4.type_01 == mon.type_01 || slot4.type_01 == mon.type_02)
                            slot4_matches["type01"]++;
                        if (slot4.type_02 == mon.type_01 || slot4.type_02 == mon.type_02)
                            slot4_matches["type02"]++;
                        if (slot4.region == mon.region)
                            slot4_matches["region"]++;
                        if (slot4.generation == mon.generation)
                            slot4_matches["gen"]++;
                        if (slot4.evo_method == mon.evo_method)
                            slot4_matches["evo_method"]++;
                        if (slot4.evo_family.Contains(mon.dex_number))
                            slot4_matches["evo_family"]++;
                    }
                    if (secretParty.Count >= 5)
                    {//count matching properties for slot 5
                        if (slot5.type_01 == mon.type_01 || slot5.type_01 == mon.type_02)
                            slot5_matches["type01"]++;
                        if (slot5.type_02 == mon.type_01 || slot5.type_02 == mon.type_02)
                            slot5_matches["type02"]++;
                        if (slot5.region == mon.region)
                            slot5_matches["region"]++;
                        if (slot5.generation == mon.generation)
                            slot5_matches["gen"]++;
                        if (slot5.evo_method == mon.evo_method)
                            slot5_matches["evo_method"]++;
                        if (slot5.evo_family.Contains(mon.dex_number))
                            slot5_matches["evo_family"]++;
                    }
                    if (secretParty.Count >= 6)
                    {//count matching properties for slot 6
                        if (slot6.type_01 == mon.type_01 || slot6.type_01 == mon.type_02)
                            slot6_matches["type01"]++;
                        if (slot6.type_02 == mon.type_01 || slot6.type_02 == mon.type_02)
                            slot6_matches["type02"]++;
                        if (slot6.region == mon.region)
                            slot6_matches["region"]++;
                        if (slot6.generation == mon.generation)
                            slot6_matches["gen"]++;
                        if (slot6.evo_method == mon.evo_method)
                            slot6_matches["evo_method"]++;
                        if (slot6.evo_family.Contains(mon.dex_number))
                            slot6_matches["evo_family"]++;
                    }
                }

                //change color of each slot that isnt a direct match
                {
                    //set color based on number of matches
                    if (slot1_matches.Any(v => v.Value > 0) && gb_Slot1.Background != Brushes.Green)
                        gb_Slot1.Background = Brushes.DarkOrange;
                    else if (gb_Slot1.Background != Brushes.Green)
                        gb_Slot1.Background = Brushes.Red;

                    if (secretParty.Count >= 2 && slot2_matches.Any(v => v.Value > 0) && gb_Slot2.Background != Brushes.Green)
                        gb_Slot2.Background = Brushes.DarkOrange;
                    else if (gb_Slot2.Background != Brushes.Green)
                        gb_Slot2.Background = Brushes.Red;

                    if (secretParty.Count >= 3 && slot3_matches.Any(v => v.Value > 0) && gb_Slot3.Background != Brushes.Green)
                        gb_Slot3.Background = Brushes.DarkOrange;
                    else if (gb_Slot3.Background != Brushes.Green)
                        gb_Slot3.Background = Brushes.Red;

                    if (secretParty.Count >= 4 && slot4_matches.Any(v => v.Value > 0) && gb_Slot4.Background != Brushes.Green)
                        gb_Slot4.Background = Brushes.DarkOrange;
                    else if (gb_Slot4.Background != Brushes.Green)
                        gb_Slot4.Background = Brushes.Red;

                    if (secretParty.Count >= 5 && slot5_matches.Any(v => v.Value > 0) && gb_Slot5.Background != Brushes.Green)
                        gb_Slot5.Background = Brushes.DarkOrange;
                    else if (gb_Slot5.Background != Brushes.Green)
                        gb_Slot5.Background = Brushes.Red;

                    if (secretParty.Count >= 6 && slot6_matches.Any(v => v.Value > 0) && gb_Slot6.Background != Brushes.Green)
                        gb_Slot6.Background = Brushes.DarkOrange;
                    else if (gb_Slot6.Background != Brushes.Green)
                        gb_Slot6.Background = Brushes.Red;
                }

                var x = cb_Slot1.SelectedItem as Pokemon;
                //fill out labels of each slot
                lbl_Name_Slot1.Content = "Name: " + slot1.name;
                lbl_Type01_Slot1.Content = lbl_Type01_Slot1.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot1_matches["type01"] + " matches";
                lbl_Type02_Slot1.Content = lbl_Type02_Slot1.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot1_matches["type02"] + " matches";
                lbl_Region_Slot1.Content = lbl_Region_Slot1.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot1_matches["region"] + " matches";
                lbl_Gen_Slot1.Content = lbl_Gen_Slot1.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot1_matches["gen"] + " matches";
                lbl_EMethod_Slot1.Content = lbl_EMethod_Slot1.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot1_matches["evo_method"] + " matches";
                lbl_Family_Slot1.Content = "Evo Family - " + slot1_matches["evo_family"] + " matches";
                if (secretParty.Count >= 2)
                {
                    lbl_Name_Slot2.Content = "Name: " + slot2.name;
                    lbl_Type01_Slot2.Content = lbl_Type01_Slot2.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot2_matches["type01"] + " matches";
                    lbl_Type02_Slot2.Content = lbl_Type02_Slot2.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot2_matches["type02"] + " matches";
                    lbl_Region_Slot2.Content = lbl_Region_Slot2.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot2_matches["region"] + " matches";
                    lbl_Gen_Slot2.Content = lbl_Gen_Slot2.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot2_matches["gen"] + " matches";
                    lbl_EMethod_Slot2.Content = lbl_EMethod_Slot2.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot2_matches["evo_method"] + " matches";
                    lbl_Family_Slot2.Content = "Evo Family - " + slot2_matches["evo_family"] + " matches";
                }
                if (secretParty.Count >= 3)
                {
                    lbl_Name_Slot3.Content = "Name: " + slot3.name;
                    lbl_Type01_Slot3.Content = lbl_Type01_Slot3.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot3_matches["type01"] + " matches";
                    lbl_Type02_Slot3.Content = lbl_Type02_Slot3.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot3_matches["type02"] + " matches";
                    lbl_Region_Slot3.Content = lbl_Region_Slot3.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot3_matches["region"] + " matches";
                    lbl_Gen_Slot3.Content = lbl_Gen_Slot3.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot3_matches["gen"] + " matches";
                    lbl_EMethod_Slot3.Content = lbl_EMethod_Slot3.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot3_matches["evo_method"] + " matches";
                    lbl_Family_Slot3.Content = "Evo Family - " + slot3_matches["evo_family"] + " matches";
                }
                if (secretParty.Count >= 4)
                {
                    lbl_Name_Slot4.Content = "Name: " + slot4.name;
                    lbl_Type01_Slot4.Content = lbl_Type01_Slot4.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot4_matches["type01"] + " matches";
                    lbl_Type02_Slot4.Content = lbl_Type02_Slot4.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot4_matches["type02"] + " matches";
                    lbl_Region_Slot4.Content = lbl_Region_Slot4.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot4_matches["region"] + " matches";
                    lbl_Gen_Slot4.Content = lbl_Gen_Slot4.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot4_matches["gen"] + " matches";
                    lbl_EMethod_Slot4.Content = lbl_EMethod_Slot4.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot4_matches["evo_method"] + " matches";
                    lbl_Family_Slot4.Content = "Evo Family - " + slot4_matches["evo_family"] + " matches";
                }
                if (secretParty.Count >= 5)
                {
                    lbl_Name_Slot5.Content = "Name: " + slot5.name;
                    lbl_Type01_Slot5.Content = lbl_Type01_Slot5.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot5_matches["type01"] + " matches";
                    lbl_Type02_Slot5.Content = lbl_Type02_Slot5.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot5_matches["type02"] + " matches";
                    lbl_Region_Slot5.Content = lbl_Region_Slot5.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot5_matches["region"] + " matches";
                    lbl_Gen_Slot5.Content = lbl_Gen_Slot5.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot5_matches["gen"] + " matches";
                    lbl_EMethod_Slot5.Content = lbl_EMethod_Slot5.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot5_matches["evo_method"] + " matches";
                    lbl_Family_Slot5.Content = "Evo Family - " + slot5_matches["evo_family"] + " matches";
                }
                if (secretParty.Count >= 6)
                {
                    lbl_Name_Slot6.Content = "Name: " + slot6.name;
                    lbl_Type01_Slot6.Content = lbl_Type01_Slot6.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot6_matches["type01"] + " matches";
                    lbl_Type02_Slot6.Content = lbl_Type02_Slot6.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot6_matches["type02"] + " matches";
                    lbl_Region_Slot6.Content = lbl_Region_Slot6.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot6_matches["region"] + " matches";
                    lbl_Gen_Slot6.Content = lbl_Gen_Slot6.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot6_matches["gen"] + " matches";
                    lbl_EMethod_Slot6.Content = lbl_EMethod_Slot6.Content.ToString().Split("\u200B")[0] + "\u200B - " + slot6_matches["evo_method"] + " matches";
                    lbl_Family_Slot6.Content = "Evo Family - " + slot6_matches["evo_family"] + " matches";
                }
            }

        }
        private void cb_Party_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //the "\u200B" character is a zero width whitespace character
            //it is used as a delimeter when the labels get updated on checking the party
            //that way the output from checking their guess only gets appended to the labels on the first guess
            //and will get updated on every subsequent guess

            if (sender.Equals(cb_Slot1))
            {
                var x = cb_Slot1.SelectedItem as Pokemon;
                if (cb_Slot1.SelectedIndex == -1)
                {
                    //reset slot 1
                    {
                        gb_Slot1.Background = Brushes.Transparent;
                        foreach (object o in slot1.Children)
                        {
                            string? name = null;
                            if (o is Label)
                            {
                                name = (o as Label).Name;
                            }
                            switch (name)
                            {
                                case "lbl_Name_Slot1":
                                    (o as Label).Content = "Name:";
                                    break;
                                case "lbl_Type01_Slot1":
                                    (o as Label).Content = "TypeA:";
                                    break;
                                case "lbl_Type02_Slot1":
                                    (o as Label).Content = "TypeB:";
                                    break;
                                case "lbl_Region_Slot1":
                                    (o as Label).Content = "Region:";
                                    break;
                                case "lbl_Gen_Slot1":
                                    (o as Label).Content = "Generation:";
                                    break;
                                case "lbl_EMethod_Slot1":
                                    (o as Label).Content = "Evo Method:";
                                    break;
                                case "lbl_Family_Slot1":
                                    (o as Label).Content = "";
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    //selection was updated, so update slot 1
                    if (gb_Slot1.Background != Brushes.Green || e.AddedItems.Count > 0)
                        gb_Slot1.Background = Brushes.Transparent;
                    lbl_Name_Slot1.Content = "Name: " + x.name + "\u200B";
                    lbl_Type01_Slot1.Content = "TypeA: " + x.type_01 + "\u200B";
                    if (x.type_02 != "")
                        lbl_Type02_Slot1.Content = "TypeB: " + x.type_02 + "\u200B";
                    else
                        lbl_Type02_Slot1.Content = "TypeB: Single type\u200B";
                    lbl_Region_Slot1.Content = "Region: " + x.region + "\u200B";
                    lbl_Gen_Slot1.Content = "Gen: " + x.generation + "\u200B";
                    lbl_EMethod_Slot1.Content = "Evo Method: " + x.evo_method + "\u200B";
                }
            }
            else if (sender.Equals(cb_Slot2))
            {
                if (cb_Slot2.SelectedIndex == -1)
                {

                    //reset slot 2
                    {
                        gb_Slot2.Background = Brushes.Transparent;
                        foreach (object o in slot2.Children)
                        {
                            string? name = null;
                            if (o is Label)
                            {
                                name = (o as Label).Name;
                            }
                            switch (name)
                            {
                                case "lbl_Name_Slot2":
                                    (o as Label).Content = "Name:";
                                    break;
                                case "lbl_Type01_Slot2":
                                    (o as Label).Content = "TypeA:";
                                    break;
                                case "lbl_Type02_Slot2":
                                    (o as Label).Content = "TypeB:";
                                    break;
                                case "lbl_Region_Slot2":
                                    (o as Label).Content = "Region:";
                                    break;
                                case "lbl_Gen_Slot2":
                                    (o as Label).Content = "Generation:";
                                    break;
                                case "lbl_EMethod_Slot2":
                                    (o as Label).Content = "Evo Method:";
                                    break;
                                case "lbl_Family_Slot2":
                                    (o as Label).Content = "";
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    if (gb_Slot2.Background != Brushes.Green || e.AddedItems.Count > 0)
                        gb_Slot2.Background = Brushes.Transparent;
                    lbl_Name_Slot2.Content = "Name: " + pokedex[cb_Slot2.SelectedIndex].name + "\u200B";
                    lbl_Type01_Slot2.Content = "TypeA: " + pokedex[cb_Slot2.SelectedIndex].type_01 + "\u200B";
                    if (pokedex[cb_Slot2.SelectedIndex].type_02 != "")
                        lbl_Type02_Slot2.Content = "TypeB: " + pokedex[cb_Slot2.SelectedIndex].type_02 + "\u200B";
                    else
                        lbl_Type02_Slot2.Content = "TypeB: Single type\u200B";
                    lbl_Region_Slot2.Content = "Region: " + pokedex[cb_Slot2.SelectedIndex].region + "\u200B";
                    lbl_Gen_Slot2.Content = "Gen: " + pokedex[cb_Slot2.SelectedIndex].generation + "\u200B";
                    lbl_EMethod_Slot2.Content = "Evo Method: " + pokedex[cb_Slot2.SelectedIndex].evo_method + "\u200B";
                }
            }
            else if (sender.Equals(cb_Slot3))
            {
                if (cb_Slot3.SelectedIndex == -1)
                {
                    //reset slot 3
                    {
                        gb_Slot3.Background = Brushes.Transparent;
                        foreach (object o in slot3.Children)
                        {
                            string? name = null;
                            if (o is Label)
                            {
                                name = (o as Label).Name;
                            }
                            switch (name)
                            {
                                case "lbl_Name_Slot3":
                                    (o as Label).Content = "Name:";
                                    break;
                                case "lbl_Type01_Slot3":
                                    (o as Label).Content = "TypeA:";
                                    break;
                                case "lbl_Type02_Slot3":
                                    (o as Label).Content = "TypeB:";
                                    break;
                                case "lbl_Region_Slot3":
                                    (o as Label).Content = "Region:";
                                    break;
                                case "lbl_Gen_Slot3":
                                    (o as Label).Content = "Generation:";
                                    break;
                                case "lbl_EMethod_Slot3":
                                    (o as Label).Content = "Evo Method:";
                                    break;
                                case "lbl_Family_Slot3":
                                    (o as Label).Content = "";
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    if (gb_Slot3.Background != Brushes.Green || e.AddedItems.Count > 0)
                        gb_Slot3.Background = Brushes.Transparent;
                    lbl_Name_Slot3.Content = "Name: " + pokedex[cb_Slot3.SelectedIndex].name + "\u200B";
                    lbl_Type01_Slot3.Content = "TypeA: " + pokedex[cb_Slot3.SelectedIndex].type_01 + "\u200B";
                    if (pokedex[cb_Slot3.SelectedIndex].type_02 != "")
                        lbl_Type02_Slot3.Content = "TypeB: " + pokedex[cb_Slot3.SelectedIndex].type_02 + "\u200B";
                    else
                        lbl_Type02_Slot3.Content = "TypeB: Single type\u200B";
                    lbl_Region_Slot3.Content = "Region: " + pokedex[cb_Slot3.SelectedIndex].region + "\u200B";
                    lbl_Gen_Slot3.Content = "Gen: " + pokedex[cb_Slot3.SelectedIndex].generation + "\u200B";
                    lbl_EMethod_Slot3.Content = "Evo Method: " + pokedex[cb_Slot3.SelectedIndex].evo_method + "\u200B";

                }
            }
            else if (sender.Equals(cb_Slot4))
            {
                if (cb_Slot4.SelectedIndex == -1)
                {
                    //reset slot 4
                    {
                        gb_Slot4.Background = Brushes.Transparent;
                        foreach (object o in slot4.Children)
                        {
                            string? name = null;
                            if (o is Label)
                            {
                                name = (o as Label).Name;
                            }
                            switch (name)
                            {
                                case "lbl_Name_Slot4":
                                    (o as Label).Content = "Name:";
                                    break;
                                case "lbl_Type01_Slot4":
                                    (o as Label).Content = "TypeA:";
                                    break;
                                case "lbl_Type02_Slot4":
                                    (o as Label).Content = "TypeB:";
                                    break;
                                case "lbl_Region_Slot4":
                                    (o as Label).Content = "Region:";
                                    break;
                                case "lbl_Gen_Slot4":
                                    (o as Label).Content = "Generation:";
                                    break;
                                case "lbl_EMethod_Slot4":
                                    (o as Label).Content = "Evo Method:";
                                    break;
                                case "lbl_Family_Slot4":
                                    (o as Label).Content = "";
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    if (gb_Slot4.Background != Brushes.Green || e.AddedItems.Count > 0)
                        gb_Slot4.Background = Brushes.Transparent;
                    lbl_Name_Slot4.Content = "Name: " + pokedex[cb_Slot4.SelectedIndex].name + "\u200B";
                    lbl_Type01_Slot4.Content = "TypeA: " + pokedex[cb_Slot4.SelectedIndex].type_01 + "\u200B";
                    if (pokedex[cb_Slot4.SelectedIndex].type_02 != "")
                        lbl_Type02_Slot4.Content = "TypeB: " + pokedex[cb_Slot4.SelectedIndex].type_02 + "\u200B";
                    else
                        lbl_Type02_Slot4.Content = "TypeB: Single type\u200B";
                    lbl_Region_Slot4.Content = "Region: " + pokedex[cb_Slot4.SelectedIndex].region + "\u200B";
                    lbl_Gen_Slot4.Content = "Gen: " + pokedex[cb_Slot4.SelectedIndex].generation + "\u200B";
                    lbl_EMethod_Slot4.Content = "Evo Method: " + pokedex[cb_Slot4.SelectedIndex].evo_method + "\u200B";
                }
            }
            else if (sender.Equals(cb_Slot5))
            {
                if (cb_Slot5.SelectedIndex == -1)
                {
                    //reset slot 5
                    {
                        gb_Slot5.Background = Brushes.Transparent;
                        foreach (object o in slot5.Children)
                        {
                            string? name = null;
                            if (o is Label)
                            {
                                name = (o as Label).Name;
                            }
                            switch (name)
                            {
                                case "lbl_Name_Slot5":
                                    (o as Label).Content = "Name:";
                                    break;
                                case "lbl_Type01_Slot5":
                                    (o as Label).Content = "TypeA:";
                                    break;
                                case "lbl_Type02_Slot5":
                                    (o as Label).Content = "TypeB:";
                                    break;
                                case "lbl_Region_Slot5":
                                    (o as Label).Content = "Region:";
                                    break;
                                case "lbl_Gen_Slot5":
                                    (o as Label).Content = "Generation:";
                                    break;
                                case "lbl_EMethod_Slot5":
                                    (o as Label).Content = "Evo Method:";
                                    break;
                                case "lbl_Family_Slot5":
                                    (o as Label).Content = "";
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    if (gb_Slot5.Background != Brushes.Green || e.AddedItems.Count > 0)
                        gb_Slot5.Background = Brushes.Transparent;
                    lbl_Name_Slot5.Content = "Name: " + pokedex[cb_Slot5.SelectedIndex].name + "\u200B";
                    lbl_Type01_Slot5.Content = "TypeA: " + pokedex[cb_Slot5.SelectedIndex].type_01 + "\u200B";
                    if (pokedex[cb_Slot5.SelectedIndex].type_02 != "")
                        lbl_Type02_Slot5.Content = "TypeB: " + pokedex[cb_Slot5.SelectedIndex].type_02 + "\u200B";
                    else
                        lbl_Type02_Slot5.Content = "TypeB: Single type\u200B";
                    lbl_Region_Slot5.Content = "Region: " + pokedex[cb_Slot5.SelectedIndex].region + "\u200B";
                    lbl_Gen_Slot5.Content = "Gen: " + pokedex[cb_Slot5.SelectedIndex].generation + "\u200B";
                    lbl_EMethod_Slot5.Content = "Evo Method: " + pokedex[cb_Slot5.SelectedIndex].evo_method + "\u200B";
                }

            }
            else if (sender.Equals(cb_Slot6))
            {
                if (cb_Slot6.SelectedIndex == -1)
                {
                    //reset slot 6
                    {
                        gb_Slot6.Background = Brushes.Transparent;
                        foreach (object o in slot6.Children)
                        {
                            string? name = null;
                            if (o is Label)
                            {
                                name = (o as Label).Name;
                            }
                            switch (name)
                            {
                                case "lbl_Name_Slot6":
                                    (o as Label).Content = "Name:";
                                    break;
                                case "lbl_Type01_Slot6":
                                    (o as Label).Content = "TypeA:";
                                    break;
                                case "lbl_Type02_Slot6":
                                    (o as Label).Content = "TypeB:";
                                    break;
                                case "lbl_Region_Slot6":
                                    (o as Label).Content = "Region:";
                                    break;
                                case "lbl_Gen_Slot6":
                                    (o as Label).Content = "Generation:";
                                    break;
                                case "lbl_EMethod_Slot6":
                                    (o as Label).Content = "Evo Method:";
                                    break;
                                case "lbl_Family_Slot6":
                                    (o as Label).Content = "";
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    if (gb_Slot6.Background != Brushes.Green || e.AddedItems.Count > 0)
                        gb_Slot6.Background = Brushes.Transparent;
                    lbl_Name_Slot6.Content = "Name: " + pokedex[cb_Slot6.SelectedIndex].name + "\u200B";
                    lbl_Type01_Slot6.Content = "TypeA: " + pokedex[cb_Slot6.SelectedIndex].type_01 + "\u200B";
                    if (pokedex[cb_Slot6.SelectedIndex].type_02 != "")
                        lbl_Type02_Slot6.Content = "TypeB: " + pokedex[cb_Slot6.SelectedIndex].type_02 + "\u200B";
                    else
                        lbl_Type02_Slot6.Content = "TypeB: Single type\u200B";
                    lbl_Region_Slot6.Content = "Region: " + pokedex[cb_Slot6.SelectedIndex].region + "\u200B";
                    lbl_Gen_Slot6.Content = "Gen: " + pokedex[cb_Slot6.SelectedIndex].generation + "\u200B";
                    lbl_EMethod_Slot6.Content = "Evo Method: " + pokedex[cb_Slot6.SelectedIndex].evo_method + "\u200B";
                }

            }
        }
        private void setVisablePartySlots(int partySize)
        {
            gb_Slot2.Visibility = Visibility.Visible;
            gb_Slot3.Visibility = Visibility.Visible;
            gb_Slot4.Visibility = Visibility.Visible;
            gb_Slot5.Visibility = Visibility.Visible;
            gb_Slot6.Visibility = Visibility.Visible;

            switch (partySize)
            {
                case 1:
                    gb_Slot2.Visibility = Visibility.Hidden;
                    gb_Slot3.Visibility = Visibility.Hidden;
                    gb_Slot4.Visibility = Visibility.Hidden;
                    gb_Slot5.Visibility = Visibility.Hidden;
                    gb_Slot6.Visibility = Visibility.Hidden;
                    break;
                case 2:
                    gb_Slot3.Visibility = Visibility.Hidden;
                    gb_Slot4.Visibility = Visibility.Hidden;
                    gb_Slot5.Visibility = Visibility.Hidden;
                    gb_Slot6.Visibility = Visibility.Hidden;
                    break;
                case 3:
                    gb_Slot4.Visibility = Visibility.Hidden;
                    gb_Slot5.Visibility = Visibility.Hidden;
                    gb_Slot6.Visibility = Visibility.Hidden;
                    break;
                case 4:
                    gb_Slot5.Visibility = Visibility.Hidden;
                    gb_Slot6.Visibility = Visibility.Hidden;
                    break;
                case 5:
                    gb_Slot6.Visibility = Visibility.Hidden;
                    break;
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
        [XmlAttribute]
        public bool baby { get; set; }
        [XmlAttribute]
        public bool final_evo { get; set; }
        [XmlAttribute]
        public string form_name { get; set; }
        [XmlAttribute]
        public bool ignore_evo_restriction { get; set; }//used to override evolution based restrictions if the mon can be directly caught
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
            this.baby = false;
            this.final_evo = false;
            this.form_name = "";
            this.ignore_evo_restriction = false;
        }
        public Pokemon(int dex_number, string name, string type_01, string type_02, string region, int generation, string evo_method, List<int> evo_family, bool is_legendry, bool baby, bool final_evo, bool is_mythical, List<int> mutuallyExclusiveDexValues, string form_name, int game, bool ignore_evo_restrictions)
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
            this.baby = false;
            this.final_evo = false;
            this.form_name = form_name;
            this.ignore_evo_restriction = ignore_evo_restriction;
        }
        public string ToString()
        {
            if (this.form_name == "")
            {
                return this.dex_number + " - " + this.name;
            }
            else
            {
                return this.dex_number + " - " + this.name + " - " + this.form_name;
            }
        }
    }
}