using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class EvaluationRegressionPolicyJsonTests
{
    [Fact]
    public void Deserialize_reads_the_checked_in_policy_as_the_default()
    {
        var path = Path.Combine(
            EvaluationRegressionGateRunner.ProjectDirectory(),
            "Evaluation",
            "quality-gate-policy.json");

        var policy = EvaluationRegressionPolicyJson.Deserialize(File.ReadAllText(path));

        Assert.Equal(EvaluationRegressionPolicy.Default, policy);
    }

    [Fact]
    public void Deserialize_reads_non_negative_limits()
    {
        var policy = EvaluationRegressionPolicyJson.Deserialize(
            """
            {
              "maxFailureRateRegressionPercentagePoints": 0.5,
              "maxAdditionalUngroundedClaimFailures": 0,
              "maxAdditionalMissingExpectedFactFailures": 2
            }
            """);

        Assert.Equal(0.5d, policy.MaximumFailureRateRegression);
        Assert.Equal(0, policy.MaximumUngroundedClaimIncrease);
        Assert.Equal(2, policy.MaximumMissingExpectedFactIncrease);
        Assert.True(policy.FailWhenBaselineOrCandidateIsMissing);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_rejects_empty_json(string json)
    {
        var exception = Assert.Throws<JsonException>(() => EvaluationRegressionPolicyJson.Deserialize(json));

        Assert.Equal("Quality gate policy JSON is empty.", exception.Message);
    }

    [Fact]
    public void Deserialize_rejects_a_json_array()
    {
        var exception = Assert.Throws<JsonException>(() => EvaluationRegressionPolicyJson.Deserialize("[]"));

        Assert.Equal("A quality gate policy must be a JSON object.", exception.Message);
    }

    [Fact]
    public void Deserialize_rejects_malformed_json()
    {
        var exception = Assert.ThrowsAny<JsonException>(() => EvaluationRegressionPolicyJson.Deserialize("{"));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void Deserialize_rejects_a_missing_field()
    {
        var exception = Assert.Throws<JsonException>(() => EvaluationRegressionPolicyJson.Deserialize(
            """
            {
              "maxFailureRateRegressionPercentagePoints": 5,
              "maxAdditionalUngroundedClaimFailures": 0
            }
            """));

        Assert.Equal(
            "The quality gate policy is missing 'maxAdditionalMissingExpectedFactFailures'.",
            exception.Message);
    }

    [Theory]
    [InlineData(
        """
        {
          "maxFailureRateRegressionPercentagePoints": -0.1,
          "maxAdditionalUngroundedClaimFailures": 0,
          "maxAdditionalMissingExpectedFactFailures": 1
        }
        """,
        "'maxFailureRateRegressionPercentagePoints' must be a non-negative number.")]
    [InlineData(
        """
        {
          "maxFailureRateRegressionPercentagePoints": 5,
          "maxAdditionalUngroundedClaimFailures": -1,
          "maxAdditionalMissingExpectedFactFailures": 1
        }
        """,
        "'maxAdditionalUngroundedClaimFailures' must be a non-negative integer.")]
    [InlineData(
        """
        {
          "maxFailureRateRegressionPercentagePoints": 5,
          "maxAdditionalUngroundedClaimFailures": 0,
          "maxAdditionalMissingExpectedFactFailures": -2
        }
        """,
        "'maxAdditionalMissingExpectedFactFailures' must be a non-negative integer.")]
    public void Deserialize_rejects_a_negative_limit(string json, string message)
    {
        var exception = Assert.Throws<JsonException>(() => EvaluationRegressionPolicyJson.Deserialize(json));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Deserialize_rejects_a_duplicate_property()
    {
        var exception = Assert.Throws<JsonException>(() => EvaluationRegressionPolicyJson.Deserialize(
            """
            {
              "maxFailureRateRegressionPercentagePoints": 5,
              "maxFailureRateRegressionPercentagePoints": 9,
              "maxAdditionalUngroundedClaimFailures": 0,
              "maxAdditionalMissingExpectedFactFailures": 1
            }
            """));

        Assert.Contains("listed more than once", exception.Message, StringComparison.Ordinal);
    }
}
