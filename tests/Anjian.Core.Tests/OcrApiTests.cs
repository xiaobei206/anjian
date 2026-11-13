using System.Text.Json;
using Anjian.Core.Api;
using Anjian.Core.Imaging;
using Anjian.Core.Ocr;
using Anjian.Core.Windows;
using NLua;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class OcrApiTests
{
    [Fact]
    public void OcrReturnsTextConfidenceAndScreenMappedBoxes()
    {
        var pipe = new FakePipe();
        var service = new FakeOcr();
        using var lua = new Lua(openLibs:true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, ocrService:service));

        var values = lua.DoString("""
            local result = OCR("image-1", "zh-cn", 0.5)
            return result.text, result.lineCount, result.lines[1].text,
                result.lines[1].confidence, result.lines[1].screenX,
                result.lines[1].screenY, result.lines[1].polygon[1].x
            """);

        Assert.Equal("欢迎登录", values[0]?.ToString());
        Assert.Equal(1, Convert.ToInt32(values[1]));
        Assert.Equal("欢迎登录", values[2]?.ToString());
        Assert.Equal(0.92, Convert.ToDouble(values[3]), 5);
        Assert.Equal(-1915, Convert.ToDouble(values[4]), 5);
        Assert.Equal(26, Convert.ToDouble(values[5]), 5);
        Assert.Equal(5, Convert.ToDouble(values[6]), 5);
        Assert.Equal("ch", service.LastLanguage);
        Assert.Equal(0.5, service.LastConfidence, 5);
    }

    [Fact]
    public void FindTextHonorsContainsExactAndConfidenceThreshold()
    {
        var pipe = new FakePipe();
        using var lua = new Lua(openLibs:true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, ocrService:new FakeOcr()));

        var values = lua.DoString("""
            local hit = FindText("image-1", "登录", "ch", 0.8, "contains")
            local miss = FindText("image-1", "登录", "ch", 0.95, "contains")
            local exact = FindText("image-1", "欢迎", "ch", 0.8, "exact")
            return hit.text, miss == nil, exact == nil
            """);

        Assert.Equal("欢迎登录", values[0]?.ToString());
        Assert.True(Convert.ToBoolean(values[1]));
        Assert.True(Convert.ToBoolean(values[2]));
    }

    [Fact]
    public void WaitTextChecksCheckpointMapsScreenAndReleasesCapturedResource()
    {
        var pipe = new FakePipe();
        using var lua = new Lua(openLibs:true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, ocrService:new FakeOcr()));

        var values = lua.DoString("""
            local hit = WaitText("登录", 0, 10, "ch", 0.5, "contains")
            return hit.text, hit.screenX, hit.screenY
            """);

        Assert.Equal("欢迎登录", values[0]?.ToString());
        Assert.Equal(-1915, Convert.ToDouble(values[1]), 5);
        Assert.Equal(26, Convert.ToDouble(values[2]), 5);
        Assert.Contains(pipe.Types, x => x=="checkpoint");
        Assert.Contains(pipe.Types, x => x=="image-release");
    }

    [Fact]
    public void OcrApiRejectsInvalidConfidenceLanguageMatchModeAndIntervals()
    {
        using var lua = new Lua(openLibs:true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(new FakePipe(), lua, ocrService:new FakeOcr()));
        var values = lua.DoString("""
            local a = pcall(OCR, "image-1", "fr", 0)
            local b = pcall(OCR, "image-1", "ch", 1.1)
            local c = pcall(FindText, "image-1", "x", "ch", 0, "prefix")
            local d = pcall(WaitText, "x", 100, 0, "ch", 0, "contains")
            return a,b,c,d
            """);
        Assert.All(values, value => Assert.False(Convert.ToBoolean(value)));
    }

    private sealed class FakePipe : ILuaCommandPipe
    {
        public List<string> Types { get; } = [];
        public void Send(string type,string? name=null,double x=0,double y=0,int milliseconds=0,string? text=null)
            => _ = Request(type,name,x,y,milliseconds,text);

        public string? Request(string type,string? name=null,double x=0,double y=0,int milliseconds=0,string? text=null)
        {
            Types.Add(type);
            return type switch
            {
                "image-size" => JsonSerializer.Serialize(new ImageSizeInfo(100,60,true,-1920,20),WindowApiJson.Options),
                "image-export-png" => Convert.ToBase64String([137,80,78,71,13,10,26,10]),
                "image-capture-screen" => "screen-1",
                _ => null
            };
        }
    }

    [Fact]
    public void WaitTextUsesPauseAwareDelayBetweenPolls()
    {
        var pipe = new FakePipe();
        var service = new FakeOcr { MatchAfterCall = 2 };
        using var lua = new Lua(openLibs:true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, ocrService:service));

        var values = lua.DoString("""
            local hit = WaitText("登录", 1000, 10, "ch", 0.5, "contains")
            return hit.text
            """);

        Assert.Equal("欢迎登录", values[0]?.ToString());
        Assert.Equal(2, pipe.Types.Count(x => x == "image-capture-screen"));
        Assert.Contains(pipe.Types, x => x == "delay");
        Assert.Equal(2, pipe.Types.Count(x => x == "checkpoint"));
    }

    private sealed class FakeOcr : IOcrRecognitionService
    {
        private int _calls;
        public int MatchAfterCall { get; init; } = 1;
        public string? LastLanguage { get; private set; }
        public double LastConfidence { get; private set; }

        public OcrRecognitionResult Recognize(ReadOnlyMemory<byte> pngBytes,string language,double minConfidence)
        {
            LastLanguage=language;
            LastConfidence=minConfidence;
            var call = Interlocked.Increment(ref _calls);
            if (call < MatchAfterCall)
                return new OcrRecognitionResult("",[],[language],[],100,60,12);
            return new OcrRecognitionResult(
                "欢迎登录",
                [new OcrTextLine("欢迎登录",0.92,5,6,30,10,[new OcrTextPoint(5,6),new OcrTextPoint(35,6),new OcrTextPoint(35,16),new OcrTextPoint(5,16)])],
                [language],[],100,60,12);
        }
        public void Dispose() { }
    }
}
