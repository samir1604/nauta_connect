using NautaManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NautaConnect.Tests.NautaManager.Tests;

public class NautaDataParserTest
{
    private readonly NautaDataParser _sut = new();

    [Fact]
    public void ParseSessionFieldFromForm_ShouldExtractOnlyHiddenInputs_FromRealEtecsaHtml()
    {
        // --- ARRANGE ---
        var htmlContent = @"
        <form class='form' action='https://secure.etecsa.net:8443//LoginServlet' method='post' id='formulario'>
            <input type='hidden' name='wlanuserip' id='wlanuserip' value='10.227.108.183'/>
            <input type='hidden' name='wlanacname' id='wlanacname' value=''/>
            <input type='hidden' name='wlanmac' id='wlanmac' value='' />
            <input type='hidden' name='firsturl' id='firsturl' value='notFound.jsp' />
            <input type='hidden' name='ssid' id='ssid' value='' />			
            <input type='hidden' name='usertype' id='usertype' value='' />
            <input type='hidden' name='gotopage' id='gotopage' value='/nauta_etecsa/LoginURL/pc_login.jsp' />
            <input type='hidden' name='successpage' id='successpage' value='/nauta_etecsa/OnlineURL/pc_index.jsp' />
            <input type='hidden' name='loggerId' id='loggerId' value='20260114155929643' />
            <input type='hidden' name='lang' id='lang' value='es_ES' />
            
            <label for='nombre'>Usuario:</label> <input name='username' id='username' type='text'>
            <input class='btn' name='Enviar' value='Aceptar' type='button'>
            
            <input type='hidden' name='CSRFHW' value='a54c4e25938d36457d64c31db31d7d23' />
        </form>";

        var parser = new NautaDataParser();

        // --- ACT ---
        var result = parser.ParseSessionFieldFromForm(htmlContent);

        // --- ASSERT ---
        // Verificamos que solo capturó los 11 campos 'hidden'
        // Ignorando el 'username' (text) y el 'Enviar' (button)
        Assert.Equal(11, result.Count);

        // Verificaciones de valores clave
        Assert.Equal("10.227.108.183", result["wlanuserip"]);
        Assert.Equal("20260114155929643", result["loggerId"]);
        Assert.Equal("a54c4e25938d36457d64c31db31d7d23", result["CSRFHW"]);

        // Verificamos que NO capturó el botón Enviar aunque tenga name
        Assert.False(result.ContainsKey("Enviar"));
    }

    [Fact]
    public void ParseSessionFromJs_ShouldExtractAllValues_WhenJsIsCorrect()
    {
        // ARRANGE
        var jsContent = "var urlParam = 'ATTRIBUTE_UUID=ABC123&CSRFHW=token456&loggerId=log789';";

        // ACT
        var result = _sut.ParseSessionFieldFromJs(jsContent);

        // ASSERT
        Assert.Equal("ABC123", result["ATTRIBUTE_UUID"]);
        Assert.Equal("token456", result["CSRFHW"]);
        Assert.Equal("log789", result["loggerId"]);
    }

    [Theory]
    [InlineData("<script>alert('Error 1');</script>", "Error 1")]
    [InlineData("<script>alert(\"Error 2\");</script>", "Error 2")]
    public void ExtractAlertMessage_ShouldWork_WithDifferentQuotes(string html, string expected)
    {
        // ACT
        var result = _sut.ExtractAlertMessageFromJs(html);

        // ASSERT
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("38:03:59", 38, 3, 59)]
    [InlineData("125:00:00", 125, 0, 0)]
    [InlineData("00:59:59", 0, 59, 59)]
    public void TryParseEtecsaTime_ShouldHandleLargeHours(string input, int h, int m, int s)
    {
        // Act
        bool success = _sut.TryParseConnectionTime(input, out TimeSpan result);

        // Assert
        Assert.True(success);
        Assert.Equal(h, (int)result.TotalHours);
        Assert.Equal(m, result.Minutes);
        Assert.Equal(s, result.Seconds);
    }
}
