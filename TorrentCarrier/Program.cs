using QBittorrent.Client;
using System;
using Serilog;

string pathFile = "../../../config.ini";
Dictionary<string, string> config = ParseIniFile(pathFile);

using var log = new LoggerConfiguration()
	.WriteTo.Console()
	.WriteTo.File(config["logsPath"], rollingInterval: RollingInterval.Year)
	.CreateLogger();


Uri URL1 = new Uri(config["url1"]);
Uri URL2 = new Uri(config["url2"]);

string login1 = config["login1"];
string password1 = config["password1"];

string login2 = config["login2"];
string password2 = config["password2"];

string blackTag = config["backTag"];

string newPathNetwork = config["newPathNetwork"];
string newPathLocal = config["newPathLocal"];
string logsPath = config["logsPath"];

QBittorrent.Client.QBittorrentClient qBittorrentClient1 = new QBittorrent.Client.QBittorrentClient(URL1);
QBittorrent.Client.QBittorrentClient qBittorrentClient2 = new QBittorrent.Client.QBittorrentClient(URL2);

// await qBittorrentClient1.LoginAsync(login1, password1);
// await qBittorrentClient2.LoginAsync(login2, password2);


var data = await qBittorrentClient1.GetTorrentListAsync();
var data2 = await qBittorrentClient2.GetTorrentListAsync();

var oldTorrents = new List<TorrentInfo?>();

//берутся все торренты из двух клиентов и добавляются в список только подходящие
foreach (var torrent1 in data)
{
	if(torrent1.Tags.Contains(blackTag))
		continue;

	if(data2.Any(t => t.Hash == torrent1.Hash))
		await qBittorrentClient1.DeleteAsync(torrent1.Hash);

	if (torrent1.CompletionOn < DateTime.Now.AddDays(-14))
		oldTorrents.Add(torrent1);
}



foreach (var torrent1 in oldTorrents)
{
	string logEntry = $"{DateTime.Now} {torrent1.Name} {torrent1.CompletionOn}";

	log.Information(logEntry);
}

    
//добавляются нужные торренты
try
{
	foreach(var torrent in oldTorrents)
	{
		//перенос файла
		await qBittorrentClient1.SetLocationAsync(torrent.Hash, newPathNetwork);

		bool isTorrentReady = false;
		while (!isTorrentReady)
		{
			var torrentInfo = await qBittorrentClient1.GetTorrentListAsync();
			var currentTorrent = torrentInfo.FirstOrDefault(t => t.Hash == torrent.Hash);
			if (currentTorrent != null && currentTorrent.State != TorrentState.Moving)
			{
				isTorrentReady = true;
			}
			else
			{
				await Task.Delay(5000);
			}
		}

		//создание магнет-ссылки
		var trackers = await qBittorrentClient1.GetTorrentTrackersAsync(torrent.Hash);

		string magnet = $"magnet:?xt=urn:btih:{torrent.Hash}&dn={Uri.EscapeDataString(torrent.Name)}";
		foreach (var tracker in trackers.Skip(3))
		{
			magnet +=($"&tr={tracker.Url}");
		}

		var addTorrentRequest = new AddTorrentUrlsRequest(new Uri(magnet))
		{
			Paused = true 
		};

		await qBittorrentClient2.AddTorrentsAsync(addTorrentRequest);
		await Task.Delay(15000);
		await qBittorrentClient2.SetLocationAsync(torrent.Hash, newPathLocal);
		await qBittorrentClient2.AddTorrentPeerAsync(torrent.Hash, config["firstClientPeerIp"]);
		await qBittorrentClient2.ResumeAsync(torrent.Hash);


		//ожидание проверки торрента
		isTorrentReady = false;
		while (!isTorrentReady)
		{
			var torrentInfo = await qBittorrentClient2.GetTorrentListAsync();
			var currentTorrent = torrentInfo.FirstOrDefault(t => t.Hash == torrent.Hash);
			Console.WriteLine(currentTorrent.State);

			if(currentTorrent.State == TorrentState.PausedDownload)
				await qBittorrentClient2.ResumeAsync(torrent.Hash);

			if (currentTorrent != null && (currentTorrent.State == TorrentState.Uploading || currentTorrent.State == TorrentState.StalledUpload))
			{
				isTorrentReady = true;
			log.Information(torrent.Name + " успешно добавлен");
			}
			else
			{
				await Task.Delay(5000); 
			}
		}
		await qBittorrentClient1.DeleteAsync(torrent.Hash);
	}
	log.Information("перемещено успешно");
}
catch (Exception ex)
{
	log.Error($"Ошибка: {ex.Message}");
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
