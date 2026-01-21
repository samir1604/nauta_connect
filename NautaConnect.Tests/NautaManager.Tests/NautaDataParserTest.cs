using ConnectionManager.Result;
using NautaManager;

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
        var value = result.Value;

        // --- ASSERT ---
        // Verificamos que solo capturó los 11 campos 'hidden'
        // Ignorando el 'username' (text) y el 'Enviar' (button)
        Assert.Equal(11, result.Value.Count);

        // Verificaciones de valores clave
        Assert.Equal("10.227.108.183", value["wlanuserip"]);
        Assert.Equal("20260114155929643", value["loggerId"]);
        Assert.Equal("a54c4e25938d36457d64c31db31d7d23", value["CSRFHW"]);

        // Verificamos que NO capturó el botón Enviar aunque tenga name
        Assert.False(value.ContainsKey("Enviar"));
    }

    [Fact]
    public void ParseSessionFromJs_ShouldExtractAllValues_WhenJsIsCorrect()
    {
        // ARRANGE
        var jsContent = "var urlParam = 'ATTRIBUTE_UUID=ABC123&CSRFHW=token456&loggerId=log789';";

        // ACT
        var result = _sut.ParseSessionFieldFromJs(jsContent);
        var value = result.Value;

        // ASSERT
        Assert.Equal("ABC123", value["ATTRIBUTE_UUID"]);
        Assert.Equal("token456", value["CSRFHW"]);
        Assert.Equal("log789", value["loggerId"]);
    }

    [Theory]
    [InlineData("<script>alert('Error 1');</script>", "Error 1")]
    [InlineData("<script>alert(\"Error 2\");</script>", "Error 2")]
    public void ExtractAlertMessage_ShouldWork_WithDifferentQuotes(string html, string expected)
    {
        // ACT
        var result = _sut.IsDocumentCleanFromJsAlert(html);

        // ASSERT
        Assert.True(result.IsFailure);   
        Assert.Equal(expected, result.Error.Message);        
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

    [Fact]
    public void ParseSessionFieldFromJs_Should_StopEarly_WhenAlertIsFound()
    {
        // --- ARRANGE ---
        // HTML que tiene un ALERT (Error) y luego datos que parecerían válidos
        string html = "<script>alert('Su cuenta ha expirado');</script> var ATTRIBUTE_UUID=123;";

        // --- ACT ---
        var result = _sut.ParseSessionFieldFromJs(html);

        // --- ASSERT ---
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.UnexpectedResponse, result.Error.Type);
        Assert.Equal("Su cuenta ha expirado", result.Error.Message);

        // Verificamos que NO se intentó parsear el UUID (el resultado es el del Alert)
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ParseSessionFieldFromJs_Should_CompleteFlow_WhenNoAlertIsPresent()
    {
        // --- ARRANGE ---
        string html = "var ATTRIBUTE_UUID=ABC123XYZ; var CSRFHW=987654;";

        // --- ACT ---
        var result = _sut.ParseSessionFieldFromJs(html);

        // --- ASSERT ---
        Assert.True(result.IsSuccess);
        Assert.Equal("ABC123XYZ", result.Value[NautaServiceKeys.ATTRIBUTE_UUIDKey]);
        Assert.Equal("987654", result.Value[NautaServiceKeys.CSRFHWKey]);
    }

    [Theory]
    [InlineData("var ATTRIBUTE_UUID=;")] // Llave sin valor
    [InlineData("var CSRFHW ;")]         // Llave sin el signo igual
    [InlineData("var loggerId=")]        // Final de cadena abrupto
    [InlineData("totalmente_otro_texto")] // No hay rastro de las llaves esperadas
    public void ParseSessionFieldFromJs_ShouldReturnParserError_WhenJsIsMalformed(string malformedJs)
    {
        // --- ACT ---
        // Ejecutamos el flujo completo que usa el Bind
        var result = _sut.ParseSessionFieldFromJs(malformedJs);

        // --- ASSERT ---
        // 1. El resultado debe ser fallo
        Assert.True(result.IsFailure);

        // 2. El tipo de error debe ser ParserError
        Assert.Equal(ErrorType.ParserError, result.Error.Type);

        // 3. El mensaje debe ser descriptivo
        Assert.Contains("No se pudo extraer", result.Error.Message);

        // 4. Verificamos el blindaje: acceder al Value debe lanzar excepción
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
