#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);

var topics = new Dictionary<string, Topic>();
var subscriptions = new Dictionary<(string Id, string Topic), Action<string>>();

foreach (string line in lines.Skip(1).Take(n))
{
    string[] p = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
    switch (p[0])
    {
        case "on":
        {
            var key = (p[1], p[2]);
            if (subscriptions.ContainsKey(key))
            {
                Console.WriteLine("already subscribed");
                break;
            }
            Action<string> handler = MakeHandler(p[1]);
            subscriptions[key] = handler;
            GetTopic(p[2]).Published += handler;
            break;
        }
        case "off":
        {
            var key = (p[1], p[2]);
            if (subscriptions.Remove(key, out Action<string>? handler))
                GetTopic(p[2]).Published -= handler;
            else
                Console.WriteLine("not subscribed");
            break;
        }
        case "emit":
        {
            Topic topic = GetTopic(p[1]);
            if (topic.ListenerCount == 0)
                Console.WriteLine("no listeners");
            else
                topic.Publish(p[2]);
            break;
        }
        case "count":
            Console.WriteLine(GetTopic(p[1]).ListenerCount);
            break;
    }
}

Topic GetTopic(string name)
{
    if (!topics.TryGetValue(name, out Topic? t))
        topics[name] = t = new Topic();
    return t;
}

static Action<string> MakeHandler(string id)
{
    int count = 0;
    return payload => Console.WriteLine($"{id}#{++count}: {payload}");
}

class Topic
{
    public event Action<string>? Published;

    public int ListenerCount => Published?.GetInvocationList().Length ?? 0;

    public void Publish(string payload) => Published?.Invoke(payload);
}
