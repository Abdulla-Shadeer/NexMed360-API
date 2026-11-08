using NexMed360Api.Common;
using NexMed360Api.Common.ApplicationErrors;

namespace NexMed360.API.Old.Tests;

public class ResultConversionTests
{
    [Fact]
    public void ReturningDataCreatesASuccess()
    {
        var patient = new PatientData("patient-1");
        Result<PatientData> result = patient;

        Assert.True(result.IsSuccess);
        Assert.Same(patient, result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FalseAndZeroAreSuccessfulValues()
    {
        Result<bool> boolean = false;
        Result<int> number = 0;

        Assert.True(boolean.IsSuccess);
        Assert.False(boolean.Data);
        Assert.Null(boolean.Error);
        Assert.True(number.IsSuccess);
        Assert.Equal(0, number.Data);
    }

    [Fact]
    public void ReturningAListKeepsItsData()
    {
        var patients = new List<PatientData> { new("patient-1") };
        Result<List<PatientData>> result = patients;

        Assert.True(result.IsSuccess);
        Assert.Same(patients, result.Data);
        Assert.Single(result.Data!);
    }

    [Fact]
    public void AnErrorCodeCreatesAFailureWithTheRegisteredDetails()
    {
        Result<PatientData> result = ErrorCode.USR_NotFound;
        var expected = ErrorRegistry.GetError(ErrorCode.USR_NotFound);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(expected.Code, result.Error!.Code);
        Assert.Equal(expected.Title, result.Error.Title);
        Assert.Equal(expected.HttpStatus, result.Error.HttpStatus);
        Assert.Equal(expected.Description, result.Error.Description);
    }

    [Fact]
    public void AnErrorCanPassBetweenDifferentResultTypes()
    {
        var error = ApiError.From(ErrorCode.SYS_Unauthorized, "You cannot read this record.", 403);
        Result<PatientData> patient = error;
        Result<List<PatientData>> patients = patient.Error!;

        Assert.False(patient.IsSuccess);
        Assert.False(patients.IsSuccess);
        Assert.Same(error, patients.Error);
        Assert.Equal(403, patients.Error!.HttpStatus);
        Assert.Equal("You cannot read this record.", patients.Error.Description);
        Assert.Null(patients.Data);
    }

    [Fact]
    public void ObjectResultsTreatErrorsAsFailures()
    {
        Result<object> fromCode = ErrorCode.USR_NotFound;
        Result<object> fromError = ApiError.From(ErrorCode.SYS_BadRequest, "Choose a search type.");

        Assert.False(fromCode.IsSuccess);
        Assert.False(fromError.IsSuccess);
        Assert.Null(fromCode.Data);
        Assert.Null(fromError.Data);
        Assert.Equal(ErrorCode.USR_NotFound, fromCode.Error!.Code);
        Assert.Equal(ErrorCode.SYS_BadRequest, fromError.Error!.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ASearchSwitchKeepsDataAndErrorsSeparate(int searchType)
    {
        Result<object> Search() => searchType switch
        {
            0 => new List<PatientData> { new("patient-1") },
            1 => new List<string> { "Organization A" },
            _ => ApiError.From(ErrorCode.SYS_BadRequest, "Choose a search type.")
        };

        var result = Search();
        Assert.Equal(searchType < 2, result.IsSuccess);
        if (searchType == 2)
        {
            Assert.Null(result.Data);
            Assert.Equal(ErrorCode.SYS_BadRequest, result.Error!.Code);
        }
        else Assert.NotNull(result.Data);
    }

    [Fact]
    public void ATypedNullPayloadStillCreatesASuccess()
    {
        PatientData? patient = null;
        Result<PatientData> result = patient;

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Null(result.Error);
        Assert.True(Result<PatientData>.Success(null).IsSuccess);
    }

    [Fact]
    public void CustomErrorDetailsDoNotChangeTheRegisteredError()
    {
        var original = ErrorRegistry.GetError(ErrorCode.SYS_Unauthorized);
        var status = original.HttpStatus;
        var description = original.Description;
        var custom = ApiError.From(ErrorCode.SYS_Unauthorized, "This action is not allowed.", 403);

        Assert.NotSame(original, custom);
        Assert.Equal(original.Title, custom.Title);
        Assert.Equal(403, custom.HttpStatus);
        Assert.Equal(status, ErrorRegistry.GetError(ErrorCode.SYS_Unauthorized).HttpStatus);
        Assert.Equal(description, ErrorRegistry.GetError(ErrorCode.SYS_Unauthorized).Description);
    }

    private sealed record PatientData(string Id);
}
