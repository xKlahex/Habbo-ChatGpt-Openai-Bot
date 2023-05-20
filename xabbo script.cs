using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;

var apiKey = "OPENAI_API_KEY";
var extravar = $"Be Friendly,smart and give cool humour answers with fresh answers and coolnes and little bit smart-ass with modern internet language. The Current Date is: '{DateTime.Today}'. The Output Language is 'English'. ";
var role = $"Your name is '{Self.Name}' and your role is to behave like a Habbo Hotel user.";
var chatInstructions = $"Answer a question from a Habbo Hotel user, but keep the response short and under 250 characters. Use modern internet language. No hashtags. {role}";
var lastQuestionTime = DateTime.MinValue;
var cooldown = TimeSpan.FromSeconds(6);
var isFloodControlled = false;
var messageQueue = new Queue<(int messenger, string message)>();
var isProcessing = false;
var blacklistedWords = new List<string> { "spell backwards", "sex", "bobba","kosovo" };

async Task<string> GetAnswerFromAPI(HttpClient httpClient, object requestBody) {
  var jsonRequest = JsonSerializer.Serialize(requestBody);
  var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
  var delayTask = Task.Delay(TimeSpan.FromSeconds(8));
  var apiTask = httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
  var completedTask = await Task.WhenAny(apiTask, delayTask);
  if (completedTask == apiTask) {
    var response = await apiTask;
    var responseContent = await response.Content.ReadAsStringAsync();
    Log($"API Response: {responseContent}");
    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
    if (jsonResponse.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0) {
      var answer = choices[0].GetProperty("message").GetProperty("content").GetString().Trim();
      var pattern = @"[^a-zA-Z0-9\s\p{P}äöüÜÄÖß+=^‡|¥ƒí—ªºµ±÷•°¡¿¶™õ©®‘¢§£éØÕ†¬»½]";
      var cleanAnswer = Regex.Replace(answer, pattern, "");
      return cleanAnswer;
    } else {
      Log("No answer found or ratelimited.");
      return "Sorry, I couldn't find an answer.";
    }
  }
  else {
    Log("Time limit exceeded.");
    return "Sorry, I couldn't find an answer.";
  }
}


bool ContainsBlacklistedWord(string message) {
  foreach (var word in blacklistedWords) {
    if (message.ToLower().Contains(word.ToLower())) {
      return true;
    }
  }
  return false;
}

OnChat(async e => {
  string userNames = string.Join(", ", Users.Select(u => u.Name));
  if (isFloodControlled) {
    Log("Flood control in progress. Please wait.");
    Sign(17);
    return;
  }
  if (!e.Message.ToLower().StartsWith("#") || e.ChatType == ChatType.Whisper) return;
  if (DateTime.UtcNow - lastQuestionTime < cooldown) {
    Log("Cooldown in progress. Please wait.");
    Sign(17);
    return;
  }
  lastQuestionTime = DateTime.UtcNow;
  var message = e.Message.Substring(1);
  
  var userProfile = await Task.Run(() => GetProfile(e.Entity.Id));

  var roomfacts = @$"
Username of the user asking the question:'{e.Entity.Name}'
,Current room name:'{Room.Name}'
,Current room description:'{Room.Description}'
,Current room owner name:'{Room.OwnerName}'
,Current room group name:'{Room.GroupName}'
,Current room event name:'{Room.EventName}'
,Current room event description:'{Room.EventDescription}'
,Current room Floor item amount:'{Room.FloorItems.Count()}'
,Current room Wall item amount:'{Room.WallItems.Count()}'
,Number of users in the room:'{Users.Count()}'
,List of all users currently in the room:'{userNames}'
,Description/motto in profile of the user asking the question:'{e.Entity.Motto}'
,Friend count of the user asking the question:'{userProfile.Friends}'
,Activity points of the user asking the question:'{userProfile.ActivityPoints}'
,Account creation date of the user asking the question:'{userProfile.Created}'
,Whether the user asking the question is a friend:'{userProfile.IsFriend}'
,Last login date of the user asking the question:'{userProfile.LastLogin}'
,Level of the user asking the question:'{userProfile.Level}'
,Star gems of the user asking the question:'{userProfile.StarGems}'
,{extravar} ";

  if (ContainsBlacklistedWord(message)) {
    Shout($"{e.Entity.Name} Your question contains a blacklisted word, if you try it again i will mute you.", 5);
    return;
  }

  switch (message.ToLower())
  {
    case string s when s.Contains("dance"):
      Dance(s.Contains("stop") ? 0 : 1);
      return;
    case "love":
      Sign(11);
      return;
    case "kiss":
      Talk("ƒ");
      Action(2);
      return;
    case "stand up":
      Talk("ok");
      Stand();
      return;
    case "sit down":
      Talk("ok");
      Sit();
      return;
    case "wave":
      Talk("*waving* Hello!!");
      Wave();
      return;
    case string s when s.Contains("follow me"):
      Talk($"okay ill catch {e.Entity.Name} :)");
      int[] dx = {-1, 1, -1, 1};
      int[] dy = {-1, 1, 1, -1};
      for (int i = 0; i < 4; i++) {
        Move(e.Entity.Location.X + dx[i], e.Entity.Location.Y + dy[i]);
        Delay(100);
      }
      return;

    default:
      if (message.ToLower().StartsWith("sign ") && int.TryParse(message.Substring(5), out int signNumber) && signNumber >= 0 && signNumber <= 14)
      {
        Sign(signNumber);
        return;
      }
      break;
  }

  if (message.ToLower().Contains("copy me")) {
    Talk($"Okay ill try to copy you {e.Entity.Name} :)");
    Send(Out["UpdateFigureData"],"M",e.Entity.Figure);
    await Task.Delay(8500);
    Send(Out["UpdateFigureData"],"M","hr-155-49.lg-280-92.sh-290-92.hd-180-1.ca-1813-1408.ch-215-92");
    return;
  }

  Send(Out["StartTyping"]);
  Log($"Question: {message}");
  await DelayAsync(1);
  var httpClient = new HttpClient {
    DefaultRequestHeaders = {
      Authorization = new AuthenticationHeaderValue("Bearer", apiKey),
      Accept = {
        new MediaTypeWithQualityHeaderValue("application/json")
      }
    }
  };
  var requestBody = new {
    model = "gpt-3.5-turbo", max_tokens = 55, temperature = 0.7, n = 1, stop = "\n", messages = new object[] {
      new {
        role = "system", content = $"{chatInstructions} {roomfacts}"
      }, new {
        role = "user", content = $"{message}"
      }
    }
  };
  var answer = await GetAnswerFromAPI(httpClient, requestBody);
  Send(Out["CancelTyping"]);
  Shout($"{answer}", 3);
});

int DelayTime() {
  return Rand(500, 1000);
}

void SendVisibleMessage(int userId, string message) {
  Delay(DelayTime());
  SendMessage(userId, message);
  Send(In.MessengerNewConsoleMessage, userId, "> " + message, 0, "");
}

OnIntercept(In["NewFriendRequest"], async p => {
  int userId = p.Packet.ReadInt();
  string userName = p.Packet.ReadString();
  AcceptFriendRequest(userId);
  Log($"{userName} added");
  await Task.Delay(DelayTime() * 10);
  SendVisibleMessage(userId, "Thank you for Adding me");
  SendVisibleMessage(userId, "Ask me anything just write");
  SendVisibleMessage(userId, "+ your_question");
});

OnIntercept(In.MessengerNewConsoleMessage, async p => {
  var messenger = p.Packet.ReadInt();
  var DM_Message_Question = p.Packet.ReadString();
  if (DM_Message_Question.StartsWith("#follow me")) {
    Send(Out["FollowFriend"],messenger);
  }
  else if (DM_Message_Question.StartsWith("#")) {
    SendVisibleMessage(messenger, "Thinking...");
    var httpClient = new HttpClient {
      DefaultRequestHeaders = {
        Authorization = new AuthenticationHeaderValue("Bearer", apiKey),
        Accept = {
          new MediaTypeWithQualityHeaderValue("application/json")
        }
      }
    };
    var requestBody = new {
      model = "gpt-3.5-turbo", max_tokens = 55, temperature = 0.7, n = 1, stop = "\n", messages = new object[] {
        new {
          role = "system", content = $"{chatInstructions}"
        }, new {
          role = "user", content = $"{DM_Message_Question}"
        }
      }
    };
    var answer = await GetAnswerFromAPI(httpClient, requestBody);
    var max_length = 125;
    if (answer.Length > max_length) {
      var chunks = Enumerable.Range(0, answer.Length / max_length)
                             .Select(i => answer.Substring(i * max_length, max_length));
      foreach (var chunk in chunks) {
        Delay(500);
        SendVisibleMessage(messenger, chunk);
      }
      if (answer.Length % max_length != 0) {
        Delay(500);
        SendVisibleMessage(messenger, answer.Substring(max_length * (answer.Length / max_length)));
      }
    } else {
      Delay(500);
      SendVisibleMessage(messenger, answer);
    }
  }
});


OnIntercept(In.SystemBroadcast, async =>
{
    Sign(13);
});

OnIntercept(In.FloodControl, async (e) =>
{
    DateTime startTime = DateTime.Now;
    var floodtimeout = e.Packet.ReadInt();
    Log($"Timeout for {floodtimeout} seconds.");
    isFloodControlled = true;

    while (DateTime.Now - startTime < TimeSpan.FromSeconds(floodtimeout))
    {
        Sign(16);
        await DelayAsync(2000); 
    }
    isFloodControlled = false;
    Sign(15);
});

OnIntercept(In.MuteTimeRemaining, async (e) =>
{
    DateTime startTime = DateTime.Now;
    var timeout = e.Packet.ReadInt();
    Log($"Timeout for {e} seconds.");
    isFloodControlled = true;

    while (DateTime.Now - startTime < TimeSpan.FromSeconds(timeout))
    {
        Sign(12);
        await DelayAsync(2000); 
    }
    isFloodControlled = false;
    Sign(15);
});

Wait();
