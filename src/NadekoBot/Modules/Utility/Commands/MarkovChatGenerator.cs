using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class MarkovChatGen : NadekoSubmodule
        {
            private Dictionary<Tuple<string, string>, int> Markov2GramMap = new Dictionary<Tuple<string, string>, int>();
            private int totalWords = 0;

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Markov()
            {
                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

                var msgs = new List<IMessage>(100);
                await Context.Channel.GetMessagesAsync(100).ForEachAsync(dled => msgs.AddRange(dled)).ConfigureAwait(false);

                var msgContent = new List<string>(100);
                var ignoreSelfID = Context.Client.CurrentUser.Id;
                foreach (var msg in msgs)
                {
                    if (msg.Author.Id != ignoreSelfID)
                        msgContent.Add(msg.Content);
                }

                Gen2Grams(msgContent);

                string sentence = "";
                for (int i = 0; i < 5; i++)
                    sentence += Weighted2GramChoice() + " ";

                await Context.Channel.SendMessageAsync(sentence).ConfigureAwait(false);
            }

            private void Gen2Grams(List<string> TextList)
            {
                foreach (var line in TextList)
                {
                    var tokens = line.Split(' ');
                    if (tokens.Length > 1)
                    {
                        for (var i = 0; i < tokens.Length - 2; i++)
                        {
                            var word1 = tokens[i];
                            var word2 = tokens[i + 1];
                            var wordTuple = new Tuple<string, string>(word1, word2);
                            if (Markov2GramMap.ContainsKey(wordTuple))
                                Markov2GramMap[wordTuple]++;
                            else
                                Markov2GramMap[wordTuple] = 1;
                            totalWords++;
                        }
                    }
                    else
                    {
                        var wordTuple = new Tuple<string, string>(tokens[0], "");
                        if (Markov2GramMap.ContainsKey(wordTuple))
                            Markov2GramMap[wordTuple]++;
                        else
                            Markov2GramMap[wordTuple] = 1;
                        totalWords++;
                    }
                }
            }

            private string Weighted2GramChoice()
            {
                var r = new NadekoRandom().Next(0, totalWords);
                var rollingtotal = 0;

                var mapIndex = 0;
                while (rollingtotal < r)
                {
                    var result = Markov2GramMap.ElementAt(mapIndex);
                    rollingtotal += result.Value;
                    if (rollingtotal >= r)
                        return result.Key.Item1 + " " + result.Key.Item2;
                    mapIndex++;
                }
                return "";
            }
                
        }
    }
}
