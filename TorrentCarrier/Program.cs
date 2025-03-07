using QBittorrent.Client;

string pathFile = "../../../config.ini";
Dictionary<string, string> config = ParseIniFile(pathFile);

Uri URL1 = new Uri(config["url1"]);
Uri URL2 = new Uri(config["url2"]);

string login1 = config["login1"];
string password1 = config["password1"];

string login2 = config["login2"];
string password2 = config["password2"];

string blackTag = config["backTag"];

string newPath = config["newPath"];
string logsPath = config["logsPath"];

QBittorrent.Client.QBittorrentClient qBittorrentClient1 = new QBittorrent.Client.QBittorrentClient(URL1);
QBittorrent.Client.QBittorrentClient qBittorrentClient2 = new QBittorrent.Client.QBittorrentClient(URL2);

await qBittorrentClient1.LoginAsync(login1, password1);
await qBittorrentClient2.LoginAsync(login2, password2);


var data = await qBittorrentClient1.GetTorrentListAsync();
var data2 = await qBittorrentClient2.GetTorrentListAsync();

var oldTorrents = new List<TorrentInfo?>();

//берутся все торренты из двух клиентов и добавляются в список только подходящие
foreach (var torrent1 in data)
{
	bool flag = true;
	foreach (var torrent2 in data2)
	{
		if (torrent2.Hash == torrent1.Hash)
		{
			flag = false;
			break;
		}
	}
	if (torrent1.CompletionOn < DateTime.Now.AddDays(-14) && !torrent1.Tags.Contains(blackTag) && flag) 
	{
		
		oldTorrents.Add(torrent1);
	}
}

using (StreamWriter writer = new StreamWriter(logsPath, append: true))
{
	foreach (var torrent1 in oldTorrents)
	{
		// Формируем строку для записи
		string logEntry = $"{DateTime.Now} {torrent1.Name} {torrent1.CompletionOn}";

		// Записываем строку в файл
		writer.WriteLine(logEntry);

		// Выводим строку в консоль (опционально)
		Console.WriteLine(logEntry);
	}
}

Console.WriteLine("Данные о торрентах успешно записаны в файл.");
    
//добавляются нужные торренты
try
{
	foreach(var torrent in oldTorrents)
	{

		await qBittorrentClient1.SetLocationAsync(torrent.Hash, newPath);
		var trackers = await qBittorrentClient1.GetTorrentTrackersAsync(torrent.Hash);

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

Dictionary<string, string> ParseIniFile(string filePath)
{
	Dictionary<string, string> iniData = new Dictionary<string, string>();

	try
	{
		string[] lines = File.ReadAllLines(filePath);

		foreach (string line in lines)
		{
			string trimmedLine = line.Trim();
			if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
				continue;
			string[] keyValue = trimmedLine.Split(new char[] { '=' }, 2);
			if (keyValue.Length == 2)
			{
				string key = keyValue[0].Trim();
				string value = keyValue[1].Trim();
				iniData[key] = value;
			}
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine("Ошибка при чтении файла: " + ex.Message);
	}

	return iniData;
}
