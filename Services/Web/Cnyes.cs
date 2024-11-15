using System.Text;
using PuppeteerSharp;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TGBot_TW_Stock_Webhook.Interface;

namespace TGBot_TW_Stock_Webhook.Services.Web
{
    public class Cnyes
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<Cnyes> _logger;
        private readonly IBrowserHandlers _browserHandlers;
        private readonly ICommonService _commonService;
        private string stockUrl = "https://www.cnyes.com/twstock/";
        private WaitForSelectorOptions waitForSelectorOptions = new WaitForSelectorOptions { Visible = true };

        public Cnyes(ITelegramBotClient botClient, ILogger<Cnyes> logger, IBrowserHandlers browserHandlers, ICommonService commonService)
        {
            _botClient = botClient;
            _logger = logger;
            _browserHandlers = browserHandlers;
            _commonService = commonService;
        }

        /// <summary>
        /// 取得K線
        /// </summary>
        /// <param name="stockNumber">股票代號</param>
        /// <param name="input">使用者輸入參數</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task GetKlineAsync(string stockNumber, Message message, CancellationToken cancellationToken, string? input = "日K")
        {
            await _commonService.RetryAsync(async () =>
            {
                try
                {
                    // 載入網頁
                    using var page = await _browserHandlers.LoadUrlAsync(stockUrl + stockNumber);

                    // 等待圖表載入，使用 CSS 選擇器
                    await page.WaitForSelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(2) div div:nth-child(2) div:nth-child(1) div div div div:nth-child(2) table", waitForSelectorOptions);

                    // 拆解元素
                    var element = await page.QuerySelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(1) div div:nth-child(2) div:nth-child(2) h2")
                                    ?? throw new Exception("找不到指定元素");
                    var textContent = await element.EvaluateFunctionAsync<string>("el => el.innerText");

                    // 股票名稱
                    var stockName = textContent.Split("\n").FirstOrDefault() ?? "未知股票";

                    // 點擊按鈕
                    await page.WaitForSelectorAsync("div.chart_range_selector button", waitForSelectorOptions);
                    var buttons = await page.QuerySelectorAllAsync("div.chart_range_selector button");
                    foreach (var btn in buttons)
                    {
                        var text = await btn.EvaluateFunctionAsync<string>("el => el.textContent");
                        if (text.Contains($"{input}"))
                        {
                            await btn.ClickAsync();
                            break;
                        }
                    }

                    // 圖表
                    _logger.LogInformation("擷取網站中...");
                    await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 20000 });
                    var chartElement = await page.WaitForSelectorAsync("div.tradingview-chart", waitForSelectorOptions)
                                          ?? throw new Exception("找不到圖表元素");

                    using Stream stream = await chartElement.ScreenshotStreamAsync();
                    await _botClient.SendPhoto(
                        caption: $"{stockName}：{input}線圖　💹",
                        chatId: message.Chat.Id,
                        photo: InputFile.FromStream(stream),
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                    _logger.LogInformation("已傳送資訊");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"GetKlineAsync:{ex.Message}");
                    throw new Exception($"GetKlineAsync:{ex.Message}");
                }


            }, message, cancellationToken);
        }

        /// <summary>
        /// 取得詳細報價
        /// </summary>
        /// <param name="stockNumber">股票代號</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task GetDetialPriceAsync(string stockNumber, Message message, CancellationToken cancellationToken)
        {
            await _commonService.RetryAsync(async () =>
            {
                try
                {
                    // 載入網頁
                    using var page = await _browserHandlers.LoadUrlAsync(stockUrl + stockNumber);

                    // 股價資訊字典保持不變
                    var InfoDic = new Dictionary<int, string>()
                {
                    { 1, "開盤價"},{ 2, "最高價"},{ 3, "成交量"},
                    { 4, "昨日收盤價"},{ 5, "最低價"},{ 6, "成交額"},
                    { 7, "均價"},{ 8, "本益比"},{ 9, "市值"},
                    { 10, "振幅"},{ 11, "迴轉率"},{ 12, "發行股"},
                    { 13, "漲停"},{ 14, "52W高"},{ 15, "內盤量"},
                    { 16, "跌停"},{ 17, "52W低"},{ 18, "外盤量"},
                    { 19, "近四季EPS"},{ 20, "當季EPS"},{ 21, "毛利率"},
                    { 22, "每股淨值"},{ 23, "本淨比"},{ 24, "營利率"},
                    { 25, "年股利"},{ 26, "殖利率"},{ 27, "淨利率"},
                };

                    // 等待圖表載入
                    await page.WaitForSelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(2) div div:nth-child(2) div:nth-child(1) div div div div:nth-child(2) table", waitForSelectorOptions);

                    _logger.LogInformation("處理相關資料...");
                    // 拆解元素
                    var element = await page.QuerySelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(1) div div:nth-child(2) div:nth-child(2) h2")
                                    ?? throw new Exception("找不到指定元素");
                    var textContent = await element.EvaluateFunctionAsync<string>("el => el.innerText");

                    // 股票名稱
                    var stockName = textContent.Split("\n").ToList()[0];

                    // 詳細報價
                    var temp_returnStockUD = await page.QuerySelectorAllAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(1) div div:nth-child(4) div:nth-child(2)");
                    if (temp_returnStockUD == null || temp_returnStockUD.Count() == 0)
                        throw new Exception("找不到詳細報價的元素");

                    var returnStockUD = await temp_returnStockUD[0].EvaluateFunctionAsync<string>("el => el.innerText");
                    var StockUD_List = returnStockUD.Split("\n");

                    // 股價相關信息
                    var stock_price = await page.QuerySelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(1) div div:nth-child(2) div:nth-child(2) div h3")
                        .EvaluateFunctionAsync<string>("el => el.innerText");
                    var stock_change_price = await page.QuerySelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(1) div div:nth-child(2) div:nth-child(2) div div div:nth-child(1) span:nth-child(1)")
                        .EvaluateFunctionAsync<string>("el => el.innerText");
                    var stock_amplitude = await page.QuerySelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(1) div div:nth-child(2) div:nth-child(2) div div div:nth-child(1) span:nth-child(2)")
                        .EvaluateFunctionAsync<string>("el => el.innerText");

                    // 選擇輸出欄位
                    var output = new int[] { 1, 2, 5 };

                    StringBuilder chart = new StringBuilder();
                    chart.AppendLine(@$"<b>-{stockName}-📝</b>");
                    chart.AppendLine(@$"<code>收盤價：{stock_price}</code>");
                    chart.AppendLine(@$"<code>漲跌幅：{stock_change_price}</code>");
                    chart.AppendLine(@$"<code>漲跌%：{stock_amplitude}</code>");

                    foreach (var i in output)
                    {
                        if (i * 2 - 1 < StockUD_List.Length)
                        {
                            chart.AppendLine(@$"<code>{InfoDic[i]}：{StockUD_List[i * 2 - 1]}</code>");
                        }
                        else
                        {
                            _logger.LogWarning($"索引 {i * 2 - 1} 超出 StockUD_List 範圍。");
                        }
                    }

                    // 圖表
                    _logger.LogInformation("擷取網站中...");
                    await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 20000 });
                    var screenshotElement = await page.WaitForSelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1)", waitForSelectorOptions)
                                            ?? throw new Exception("找不到截圖元素");

                    using Stream stream = await screenshotElement.ScreenshotStreamAsync();
                    await _botClient.SendPhoto(
                        caption: chart.ToString(),
                        chatId: message.Chat.Id,
                        photo: InputFile.FromStream(stream),
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                    _logger.LogInformation("已傳送資訊");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"GetDetialPriceAsync:{ex.Message}");
                    throw new Exception($"GetDetialPriceAsync:{ex.Message}");
                }
            }, message, cancellationToken);
        }

        /// <summary>
        /// 取得績效
        /// </summary>
        /// <param name="stockNumber">股票代號</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task GetPerformanceAsync(string stockNumber, Message message, CancellationToken cancellationToken)
        {
            await _commonService.RetryAsync(async () =>
            {
                try
                {
                    using var page = await _browserHandlers.LoadUrlAsync(stockUrl + stockNumber);

                    // 點選 cookie 提示按鈕
                    var cookiebutton = await page.QuerySelectorAsync("#__next > div._1GCLL > div > button._122qv");
                    if (cookiebutton != null)
                        await cookiebutton.ClickAsync();

                    // 滾動網頁至最下方，觸發 js
                    await page.EvaluateFunctionAsync<object>(@"() => {
                        window.scrollTo({
                            top: document.body.scrollHeight,
                            behavior: 'smooth'
                        });
                    }");

                    // 等待圖表載入
                    await page.WaitForSelectorAsync("div:nth-child(4) div:nth-child(3) section div:nth-child(2) section div:nth-child(2) div:nth-child(1) div div:nth-child(2) div", waitForSelectorOptions);
                    await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions { Timeout = 20000 });

                    // 等待數據載入
                    await page.WaitForSelectorAsync("div:nth-child(4) div:nth-child(3) section div:nth-child(2) section div:nth-child(2) div:nth-child(2) div div table", waitForSelectorOptions);

                    // 拆解元素
                    var element = await page.QuerySelectorAsync("div:nth-child(4) div:nth-child(2) div:nth-child(1) div:nth-child(1) div:nth-child(1) div div:nth-child(2) div:nth-child(2) h2")
                                    ?? throw new Exception("找不到指定元素");
                    var textContent = await element.EvaluateFunctionAsync<string>("el => el.innerText");

                    // 股票名稱
                    var stockName = textContent.Split("\n").FirstOrDefault() ?? "未知股票";

                    // 股價截圖
                    var priceElement = await page.WaitForSelectorAsync("#tw-stock-tabs div:nth-child(2) section", waitForSelectorOptions)
                                        ?? throw new Exception("找不到價格元素");

                    _logger.LogInformation("擷取網站中...");
                    using var stream = await priceElement.ScreenshotStreamAsync();
                    await _botClient.SendPhoto(
                        caption: $"{stockName} 績效表現　✨",
                        chatId: message.Chat.Id,
                        photo: InputFile.FromStream(stream),
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                    _logger.LogInformation("已傳送資訊");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"GetPerformanceAsync:{ex.Message}");
                    throw new Exception($"GetPerformanceAsync:{ex.Message}");
                }
            }, message, cancellationToken);
        }

        /// <summary>
        /// 取得新聞
        /// </summary>
        /// <param name="stockNumber">股票代號</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task GetNewsAsync(string stockNumber, Message message, CancellationToken cancellationToken)
        {
            await _commonService.RetryAsync(async () =>
            {
                try
                {
                    //載入網頁
                    using var page = await _browserHandlers.LoadUrlAsync(stockUrl + stockNumber);

                    //拆解元素 - 使用 CSS 選擇器
                    var element = await page.QuerySelectorAsync("div.quote-header h2");
                    if (element == null) throw new Exception("找不到指定元素");
                    var textContent = await element.EvaluateFunctionAsync<string>("el => el.innerText");

                    //股票名稱
                    var stockName = textContent.Split("\n").ToList()[0];

                    //���位新聞版塊 - 使用 class 選擇器
                    var newsList = await page.QuerySelectorAsync("div.news-notice-container-summary");
                    if (newsList == null) throw new Exception("找不到指定元素");

                    //選取所有新聞連結
                    var newsContent = await page.QuerySelectorAllAsync("a.container.shadow");
                    if (newsContent == null) throw new Exception("找不到指定元素");

                    var InlineList = new List<IEnumerable<InlineKeyboardButton>>();
                    for (int i = 0; i < 5; i++)
                    {
                        if (newsContent[i] == null) continue;
                        var text = await newsContent[i].EvaluateFunctionAsync<string>("el => el.textContent") ?? string.Empty;
                        var url = await newsContent[i].EvaluateFunctionAsync<string>("el => el.href") ?? string.Empty;
                        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(text)) continue;
                        InlineList.Add(new[] { InlineKeyboardButton.WithUrl(text, url) });
                    }

                    InlineKeyboardMarkup inlineKeyboard = new(InlineList);
                    var s = inlineKeyboard.InlineKeyboard;
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: @$"⚡️{stockName}-即時新聞",
                        replyMarkup: inlineKeyboard,
                        cancellationToken: cancellationToken);
                    _logger.LogInformation("已傳送資訊");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"GetNewsAsync:{ex.Message}");
                    throw new Exception($"GetNewsAsync:{ex.Message}");
                }
            }, message, cancellationToken);
        }
    }
}