using CliWrap;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace Reddit_Video_Downloader_Telegram_Bot
{
    class Program
    {
        static ITelegramBotClient bot;
        static HttpClient client = new HttpClient();
        static string url;
        static JArray json;
        static Message editedMessage;
        enum Mode { RESOLUTION, VIDEO, AUDIO, SOUNDLESS };
        static Mode mode;


        static void Main()
        {
            //Reddit Video bot token
            bot = new TelegramBotClient("1062789541:AAFY99TtmIaiqwdNb07VNMPgzQj6sneylPY"); 

            var me = bot.GetMeAsync().Result;
            Console.WriteLine(
              $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
            );
            bot.OnMessage += Bot_OnMessage;
            bot.OnCallbackQuery += BotOnCallbackQueryReceivedAsync;
            bot.StartReceiving();
            Thread.Sleep(int.MaxValue);
        }


        static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            Message message = e.Message;
            Chat chat = e.Message.Chat;
            if (message.Text != null)
            {
                Console.WriteLine($"Received a text message from {chat.FirstName}(@{chat.Username}) in chat {chat.Id}. It says {message.Text}");

                if (message.Text == "/start")
                {
                    await bot.SendTextMessageAsync(chat, "Please send me link to reddit video");
                }
                else
                {
                    if (message.Text.Contains("reddit") || message.Text.Contains("redd.it"))
                    {
                        url = message.Text;

                        HttpResponseMessage response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        url = response.RequestMessage.RequestUri.ToString();
                        //Console.WriteLine(url);

                        string jsonData = await client.GetStringAsync(url + ".json");

                        json = JArray.Parse(jsonData);

                        bool isRedditMediaDomain = Convert.ToBoolean(json[0]["data"]["children"][0]["data"]["is_reddit_media_domain"]);

                        if (isRedditMediaDomain == true)
                        {
                            InlineKeyboardMarkup inlineKeyboardDownloadMode = new InlineKeyboardMarkup(
                               new[]
                               {
                        InlineKeyboardButton.WithCallbackData("Video"),
                        InlineKeyboardButton.WithCallbackData("Audio"),
                        InlineKeyboardButton.WithCallbackData("GIF")
                               }
                               );

                            editedMessage = await bot.SendTextMessageAsync(chat, "What do you want to download?",
                                           replyMarkup: inlineKeyboardDownloadMode
                                          );

                            //Console.WriteLine(videoUrl);
                            //await Download(videoUrl, title, ".mp4");
                            //await Download(audioUrl, title, ".mp3");
                        }
                        else
                        {
                            await bot.SendTextMessageAsync(chat, "We don't accept embedded links");
                        }
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chat, "Link must be from reddit.com");
                    }
                }

            }
        }

        static async void BotOnCallbackQueryReceivedAsync(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {

            var callbackQuery = callbackQueryEventArgs.CallbackQuery;
            long chatId = callbackQuery.Message.Chat.Id;
            await bot.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                $"Received {callbackQuery.Data}");

            switch (callbackQuery.Data)
            {
                case "GIF":
                    mode = Mode.SOUNDLESS;
                    break;
                case "Video":
                    mode = Mode.RESOLUTION;
                    //chooseRes = true;
                    //videoMode = true;
                    break;
                case "Audio":
                    mode = Mode.AUDIO;
                    //audioMode = true;
                    break;
                case "240p":
                case "360p":
                case "480p":
                case "720p":
                    mode = Mode.VIDEO;
                    break;
                default:

                    break;
            }



            if (mode == Mode.RESOLUTION)
            {
                InlineKeyboardMarkup inlineKeyboardResolutions = new InlineKeyboardMarkup(new[]
                {
                   new[]
                   {
                        InlineKeyboardButton.WithCallbackData("720p")
                   },
                   new[]
                   {
                       InlineKeyboardButton.WithCallbackData("480p")
                   },
                   new[]
                   {
                       InlineKeyboardButton.WithCallbackData("360p")
                   },
                   new[]
                   {
                       InlineKeyboardButton.WithCallbackData("240p")
                   }
                }
                );

                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId, "Choose resolution",
                                           replyMarkup: inlineKeyboardResolutions
                                          );

                //mode = Mode.VIDEO;
            }
            else if (mode == Mode.VIDEO)
            {

                // Ascending
                // DASH_600_K / DASH_1_2_M / DASH_2_4_M / DASH_4_8_M
                // DASH_240 / DASH_360 / DASH_480 / DASH_720


                //if (videoUrl.Contains("DASH_240") || videoUrl.Contains("DASH_360") 
                //    || videoUrl.Contains("DASH_480") || videoUrl.Contains("DASH_720") || videoUrl.Contains("DASH_1080"))
                //{
                //    switch (callbackQuery.Data)
                //    {
                //        case "240p":
                //            mediaBaseUrl += "/DASH_240";
                //            break;
                //        case "360p":
                //            mediaBaseUrl += "/DASH_360";
                //            break;
                //        case "480p":
                //            mediaBaseUrl += "/DASH_480";
                //            break;
                //        case "720p":
                //            mediaBaseUrl += "/DASH_720";
                //            break;
                //    }
                //}
                //else if (videoUrl.Contains("DASH_600_K") || videoUrl.Contains("DASH_1_2_M") 
                //        || videoUrl.Contains("DASH_2_4_M") || videoUrl.Contains("DASH_4_8_M") || videoUrl.Contains("DASH_9_6_M"))
                //{
                //    switch (callbackQuery.Data)
                //    {
                //        case "240p":
                //            mediaBaseUrl += "/DASH_600_K";
                //            break;
                //        case "360p":
                //            mediaBaseUrl += "/DASH_1_2_M";
                //            break;
                //        case "480p":
                //            mediaBaseUrl += "/DASH_2_4_M";
                //            break;
                //        case "720p":
                //            mediaBaseUrl += "/DASH_4_8_M";
                //            break;
                //    }
                //}             


                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId, "Wait for the video to be downloaded and sent to you"
                        );

                string videoUrl = json[0]["data"]["children"][0]["data"]["media"]["reddit_video"]["fallback_url"].ToString();
                string audioUrl = json[0]["data"]["children"][0]["data"]["url"].ToString() + "/audio";
                string title = json[0]["data"]["children"][0]["data"]["title"].ToString();



                int videoHeight = 0; 
                switch (callbackQuery.Data)
                {
                    case "240p":
                        videoHeight = 240;
                        break;
                    case "360p":
                        videoHeight = 360;
                        break;
                    case "480p":
                        videoHeight = 480;
                        break;
                    case "720p":
                        videoHeight = 720;
                        break;
                }


                string videoFileName = $"video_{chatId}.mp4";
                string audioFileName = $"audio_{chatId}.mp3";

                bool downloadResultVideo = await Download(videoUrl, videoFileName);
                bool downloadResultAudio = await Download(audioUrl, audioFileName);



                if (downloadResultAudio == true && downloadResultVideo == true)
                {
                    string muxedFileName = "muxed_" + videoFileName;
                    var result = await Cli.Wrap("ffmpeg.exe")
                                .WithArguments($"-i {videoFileName} -i {audioFileName} {muxedFileName}")
                                .ExecuteAsync();

                    if (result.RunTime.TotalSeconds > 0)
                    {
                        using (var stream = System.IO.File.OpenRead(muxedFileName))
                        {
                            await bot.SendVideoAsync(chatId, stream, caption: title, height: videoHeight);
                        }
                        System.IO.File.Delete(videoFileName);
                        System.IO.File.Delete(audioFileName);
                        System.IO.File.Delete(muxedFileName);
                    }
                }
                else if (downloadResultVideo == true)
                {
                    InputOnlineFile inputOnlineFile = new InputOnlineFile(videoUrl);
                    await bot.SendVideoAsync(chatId, inputOnlineFile, caption: title, height: videoHeight);
                    System.IO.File.Delete(videoFileName);
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId, "Request failed. The link has to be a reddit post with video");
                }
                //videoResolution = string.Empty;
                //videoMode = false;
            }
            else if (mode == Mode.AUDIO)
            {

                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId,
                    "Wait for the audio to be downloaded and sent to you");

                string audioUrl = json[0]["data"]["children"][0]["data"]["url"].ToString() + "/audio";
                string title = json[0]["data"]["children"][0]["data"]["title"].ToString();

               // await bot.SendTextMessageAsync(chatId, "Audio is being sent to you");
                //Uploading file by passing Url
                InputOnlineFile inputOnlineFile = new InputOnlineFile(audioUrl);
                await bot.SendAudioAsync(chatId, inputOnlineFile, caption: title);

            }
            else if (mode == Mode.SOUNDLESS)
            {

                await bot.EditMessageTextAsync(chatId, editedMessage.MessageId, "Wait for the video to be downloaded and sent to you"
                        );
                string videoUrl = json[0]["data"]["children"][0]["data"]["media"]["reddit_video"]["fallback_url"].ToString();
                string title = json[0]["data"]["children"][0]["data"]["title"].ToString();
                InputOnlineFile inputOnlineFile = new InputOnlineFile(videoUrl);
                await bot.SendVideoAsync(chatId, inputOnlineFile, caption: title);

            }
            

        }
        static async Task<bool> Download(string url, string fileName)
        {
            using (HttpResponseMessage response = await client.GetAsync(url))
            {
                if (response.IsSuccessStatusCode)
                {
                    using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                    {
                        using (FileStream DestinationStream = System.IO.File.Create(fileName))
                        {
                            await streamToReadFrom.CopyToAsync(DestinationStream);
                        }
                    }
                    Console.WriteLine("Downloaded");
                    return true;
                }
            }
            return false;
        }
    } 
}
