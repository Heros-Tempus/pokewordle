
# Pokewordle

An app to facilitate a wordle style pokemon challenge run. The challenge is to play and beat a pokemon game with a hidden party that this app generates. The catch is you are only allowed to guess when you beat a gym/elite 4. Although the app has no way of enforcing this condition, so it's up to you how often you submit a guess.

## How to play
1) You pick your game in the top right, adjust any party settings you want, then hit the "Generate Party" button to generate the hidden party
    - The hidden party and settings are both saved to the Resources folder. They are saved as unencrypted text files so you could look at them in any text editor. But, they are saved without file extensions as a roundabout way to get windows to confirm that you actually want to open these files because looking at the party file would be cheating and editing the settings file could break the logic of the game and make it impossible to submit a correct guess.
2) Select a Pokemon for each slot of the party and hit the "Submit Guess" button
    - Slots that match something in the hidden party will be marked green
    - Slots that do not match, but have at least one matching property will be marked orange
    - Slots that do not have any properties in common with anything on the hidden party will be marked red
    - Each property in each slot will display the total number of matches there are in the hidden party
3) Repeat step 2 until all slots are green

## Game assumptions
This app is built specifically with the aformentioned challenge run in mind, so there are a few assumptions built into the logic to keep in mind.
  1) Pokemon will be excluded from the list of available guesses if they are not available in the selected game
  2) Pokemon that normally evolve via trade are considered available if there is some way to get them without trading
  3) Legendry and mythical pokemon are excluded by default as I considered them too strong
  4) Baby pokemon are excluded by default as I considered them too weak
  5) The hidden party will not include duplicates by default
  6) Pokemon who are part of a mutually exclusive set are treated as matching the others in the same set when submitting your guess by default
     - e.g. In gen one, there is only one eevee. You cannot catch more, and breeding doesn't exist yet. Because of these restrictions, if there is an eevee on the party it is impossible to have any of its evolutions without trading or cheating. So, if the selected game is a gen 1 game and there is a jolteon in the hidden party and you guess vaporeon then the app will count it as a match because if you were playing by the rules of the challenge then guessing vaporeon would mean you have got a vaporeon on your team and locked yourself out of getting a jolteon.

## Dataset structure
The first 13 columns of the dataset are used to define the National Pokedex and can be thought of as the most basic form of every individual pokemon. Every column that follows define the specifics of each game and is used by the app to translate the National Pokedex into an appropriate Regional Pokedex for the game you selected.

If you are so inclined then you could add your own Pokemon, or define your own Regional Dex.  
(I do not recommend this as it is very **very** tedious)

### Adding your own Pokemon
All you have to do is and fill out a new row to the dataset.

### Adding your own game
Firstly, be sure to add the game title to the first row of the dataset because this is what signals the app to read that column of the dataset.
For every row in this new column, if there are specific properties you want the Pokemon on that row to have in that game then you have to define the new properties in the game's column.
There are 5 keywords that the app checks for in this column, each with their own value pair. You can use some, all, or none of these keywords.

 - The format to use these keywords together is: keyword=value\~keyword=value\~keyword=value

##### Available
This keyword is used to indicate that the Pokemon is not available.  
The only value that the app is looking for with this keyword is FALSE. Any other value does nothing.

##### Catchable
This keyword is used to indicate that there exists some other means to obtain a trade evolution Pokemon, whether it be via in-game trade or they are directly catchable.  
If the value is set to TRUE, then the app will not exclude that Pokemon if it is a trade evolution.

##### Mutually Exclusive
This keyword is used to define a mutually exclusive set.  
The required value for this keyword is a list of Pokedex numbers delimited by semicolons. Keep in mind that the Dex numbers in this app are zero indexed.

##### Override
This keyword is used to override any/all of the Pokemon's properties from the National Dex.  
The value paired with this keyword is a list of key/value pairs where the key is the name of the property being overriden and the value is the new value for that property
 - the format used for this list of key/value pairs is: key-value&key-value&key-value

##### Alt Forms
This keyword is used to define any alternate forms of a Pokemon that exist within the game.
The value of this keyword is a list of lists of key/value pairs defining all properties of all alternate forms available in the game (see Wormadam in the dataset for an example).
 - the format used for this list of lists of key/value pairs is:<br> listOneKey-listOneValue&listOneKey-listOneValue|listTwoKey-listTwoValue&listTwoKey-listTwoValue




>The dataset, pokemon_dataset.csv, is derived from the dataset found at https://www.kaggle.com/datasets/ceebloop/pokmon-dataset-2024
