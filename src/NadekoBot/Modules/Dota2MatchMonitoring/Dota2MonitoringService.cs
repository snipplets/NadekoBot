using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using NadekoBot.Extensions;
using NadekoBot.Attributes;
using Discord.Commands;
using NadekoBot.Services;
using NadekoBot.Services.Models;

namespace NadekoBot.Modules.Dota2Mon
{
    [NadekoModule("Dota2Mon", "~")]
    public partial class Dota2MonitoringService : NadekoTopLevelModule
    {
        public Dota2MonitoringService()
        {
            Task.Run(async() =>
            {
                while (true)
                {
                    using (var uow = DbHandler.UnitOfWork())
                    {
                        var profiles = uow.Dota2.GetAll();
                        await uow.CompleteAsync();

                        //future optimizations, collate a list of all matches to be checked against the db to minimise queries
                        foreach (var profile in profiles)
                        {
                            using (var http = new HttpClient())
                            {
                                var response = await http.GetStringAsync($"https://api.opendota.com/api/players/{profile.steamID}/matches?limit=1");
                                var matches = JsonConvert.DeserializeObject<List<Dota2MatchSummaries>>(response);
                                uow.Dota2Matches.AddNoUpdate(matches[0].match_id);
                                await uow.CompleteAsync();
                            }
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    using (var uow = DbHandler.UnitOfWork())
                    {
                        var unparsedmatches = uow.Dota2Matches.GetUnparsed();
                        await uow.CompleteAsync();

                        foreach (var match in unparsedmatches)
                        {
                            using (var http = new HttpClient())
                            {
                                var response = await http.GetStringAsync($"https://api.opendota.com/api/matches/{match.match_id}");
                                var players = JsonConvert.DeserializeObject<Dota2MatchResults>(response);

                                var embed = new Discord.EmbedBuilder()
                                    .WithTitle($":fire: Someone Played Dota :fire:")
                                    .WithUrl($"https://www.opendota.com/matches/{match.match_id}");

                                string WinnerText = "";

                                foreach (var player in players.Players)
                                {
                                    var linkedacc = uow.Dota2.FindSteamID(player.AccountId);
                                    await uow.CompleteAsync();

                                    if (linkedacc != null)
                                    {
                                        if (player.Win == 1)
                                        {
                                            // Award for winning
                                            await CurrencyHandler.AddCurrencyAsync(linkedacc.UserId, $"Awarded for winning a video game.", 5).ConfigureAwait(false);
                                            WinnerText += $"{linkedacc.Username}\t\t +5 for Winning\n";
                                        }

                                        if (player.Win == 0)
                                        {
                                            // Award for playing
                                            await CurrencyHandler.AddCurrencyAsync(linkedacc.UserId, $"Awarded for playing a video game.", 1).ConfigureAwait(false);
                                            WinnerText += $"{linkedacc.Username}\t\t +1 for Playing\n";
                                        }
                                    }

                                }
                                embed.AddField(fb => fb.WithName("Winners!").WithValue(WinnerText).WithIsInline(false));
                                var Channel = uow.Dota2Channels.Get(1);
                                var Context = NadekoBot.Client.GetGuild(Channel.guild_id)?.GetTextChannel(Channel.channel_id);

                                await Context.EmbedAsync(embed).ConfigureAwait(false);
                                uow.Dota2Matches.UpdateByMatchID(match.match_id);
                                await uow.CompleteAsync();
                            }
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            });
        }
    }
}
