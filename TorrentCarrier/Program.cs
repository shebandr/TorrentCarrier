using QBittorrent.Client;
Uri URL1 = new Uri("http://127.0.0.1:10305");
Uri URL2 = new Uri("http://192.168.1.130:8080");

string login1 = "admin";
string password1 = "adminadmin";

string login2 = "admin";
string password2 = "adminadmin";

string blackTag = "GOIDA";

string newPath = "D:\\1";


QBittorrent.Client.QBittorrentClient qBittorrentClient = new QBittorrent.Client.QBittorrentClient(URL1);
QBittorrent.Client.QBittorrentClient qBittorrentClient2 = new QBittorrent.Client.QBittorrentClient(URL2);

await qBittorrentClient.LoginAsync(login1, password1);
await qBittorrentClient2.LoginAsync(login2, password2);


var data = await qBittorrentClient.GetTorrentListAsync();
var data2 = await qBittorrentClient2.GetTorrentListAsync();
var oldTorrents = new List<TorrentInfo?>();
foreach (var log in data)
{
	/*	Console.WriteLine(log.Name + " " + log.Tags + " " + log.CompletionOn);*/
	bool flag = true;
	foreach (var torrent in data2)
	{
		if (torrent.Hash == log.Hash)
		{
			flag = false;
			break;
		}
	}
	if (log.CompletionOn < DateTime.Now.AddDays(-14) && !log.Tags.Contains(blackTag) && true) 
	{
		
		oldTorrents.Add(log);
	}
}
Console.WriteLine("\n\n\n" + data.Count + " " + oldTorrents.Count +  "\n\n");
foreach (var log in oldTorrents)
{
	Console.WriteLine(log.Name + " " + log.Tags + " " + log.CompletionOn);
}

try
{
	foreach(var torrent in oldTorrents)
	{

		//await qBittorrentClient.SetLocationAsync(torrent.Hash, newPath);
		var trackers = await qBittorrentClient.GetTorrentTrackersAsync(torrent.Hash);

		string magnet = $"magnet:?xt=urn:btih:{torrent.Hash}&dn={Uri.EscapeDataString(torrent.Name)}";
		foreach (var tracker in trackers)
		{
			magnet +=($"&tr={tracker.Url}");
		}

		await qBittorrentClient2.AddTorrentsAsync(new AddTorrentUrlsRequest(new Uri(magnet)));
	}
	Console.WriteLine("перемещено успешно");
}

catch (Exception ex)
{
	Console.WriteLine($"Ошибка: {ex.Message}");
}