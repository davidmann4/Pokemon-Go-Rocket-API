using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Helpers;

namespace PokemonGo.RocketAPI.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(() => Execute());
             System.Console.ReadLine();
        }

        static async void Execute()
        {
            var client = new Client(Settings.DefaultLatitude, Settings.DefaultLongitude);

            System.Console.WriteLine("booting");

            //await client.LoginGoogle();

            await client.LoginPtc("username", "password");
            //await client.LoginGoogle(Settings.DeviceId, Settings.Email, Settings.LongDurationToken);
            var serverResponse = await client.GetServer();
            var profile = await client.GetProfile();
            var settings = await client.GetSettings();
            var mapObjects = await client.GetMapObjects();
            var inventory = await client.GetInventory();
            var pokemons = inventory.Payload[0].Bag.Items.Select(i => i.Item?.Pokemon).Where(p => p != null && p?.PokemonId != InventoryResponse.Types.PokemonProto.Types.PokemonIds.PokemonUnset);
            
            System.Console.WriteLine("starting farm");
            await ExecuteFarmingPokestopsAndPokemons(client);
            //await ExecuteCatchAllNearbyPokemons(client);

        }



        private static async Task ExecuteFarmingPokestopsAndPokemons(Client client)
        {
            var mapObjects = await client.GetMapObjects();

            var pokeStops = mapObjects.Payload[0].Profile.SelectMany(i => i.Fort).Where(i => i.FortType == (int)MiscEnums.FortType.CHECKPOINT && i.CooldownCompleteMs < DateTime.UtcNow.ToUnixTime());

            foreach (var pokeStop in pokeStops)
            {
                var update = await client.UpdatePlayerLocation(pokeStop.Latitude, pokeStop.Longitude);
                System.Console.WriteLine($"jump to fort pos - lat: { pokeStop.Latitude}, long: {pokeStop.Longitude}");
                var fortInfo = await client.GetFort(pokeStop.FortId, pokeStop.Latitude, pokeStop.Longitude);
                var fortSearch = await client.SearchFort(pokeStop.FortId, pokeStop.Latitude, pokeStop.Longitude);
                var bag = fortSearch.Payload[0];

                System.Console.WriteLine($"Farmed XP: {bag.XpAwarded}, Gems: { bag.GemsAwarded}, Eggs: {bag.EggPokemon} Items: {GetFriendlyItemsString(bag.Items)}");

                await ExecuteCatchAllNearbyPokemons(client);

                await Task.Delay(5000);
            }
        }

        private static async Task ExecuteCatchAllNearbyPokemons(Client client)
        {
            var mapObjects = await client.GetMapObjects();

            var pokemons = mapObjects.Payload[0].Profile.SelectMany(i => i.MapPokemon);

            foreach (var pokemon in pokemons)
            {
                var update = await client.UpdatePlayerLocation(pokemon.Latitude, pokemon.Longitude);
                System.Console.WriteLine($"jump to pokemon pos - lat: { pokemon.Latitude}, long: {pokemon.Longitude}");
                var encounterPokemonRespone = await client.EncounterPokemon(pokemon.EncounterId, pokemon.SpawnpointId);

                CatchPokemonResponse caughtPokemonResponse;
                do
                {
                    caughtPokemonResponse = await client.CatchPokemon(pokemon.EncounterId, pokemon.SpawnpointId, pokemon.Latitude, pokemon.Longitude);
                } 
                while(caughtPokemonResponse.Payload[0].Status == 2);

                System.Console.WriteLine(caughtPokemonResponse.Payload[0].Status == 1 ? $"We caught a {pokemon.PokedexTypeId}" : $"{pokemon.PokedexTypeId} got away..");
                await Task.Delay(5000);
            }
        }

        private static string GetFriendlyItemsString(IEnumerable<FortSearchResponse.Types.Item> items)
        {
            var sb = new StringBuilder();
            foreach(var item in items)
                sb.Append($"{item.ItemCount} x {(MiscEnums.Item)item.Item_}, ");

            return sb.ToString();
        }
    }
}
