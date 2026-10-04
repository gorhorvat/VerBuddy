using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class AttemptFlowTests(ApiFactory factory)
{
    [Fact]
    public async Task CorrectSubmission_AutoGradesAndAwardsXp()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync(xpReward: 50);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var start = await client.StartAsync(assignment.Id);
        var result = await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        Assert.Equal(AttemptStatus.Completed, result.Status);
        Assert.Equal(1, result.Score);
        Assert.Equal(1, result.MaxScore);
        Assert.Equal(50, result.EarnedXp);
    }

    [Fact]
    public async Task SecondStart_ResumesSameAttempt_AndLocksAfterSubmit()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var first = await client.StartAsync(assignment.Id);
        var second = await client.StartAsync(assignment.Id);
        Assert.Equal(first.AttemptId, second.AttemptId); // Resume, not a new attempt.

        await client.SubmitAsync(assignment.Id, first.Questions[0].Id, new { selectedIndex = 0 });

        var afterSubmit = await client.PostAsync($"/api/student/assignments/{assignment.Id}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, afterSubmit.StatusCode); // One-attempt lock.
    }

    [Fact]
    public async Task LateSubmission_IsInvalidated_WithZeroXp()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync(timeLimitSeconds: 5);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var start = await client.StartAsync(assignment.Id);

        // Backdate the attempt past limit + grace instead of sleeping.
        await factory.WithDbAsync(async db =>
        {
            var attempt = await db.StudentAttempts.SingleAsync(a => a.Id == start.AttemptId);
            attempt.StartedAt = DateTime.UtcNow.AddSeconds(-30);
            await db.SaveChangesAsync();
        });

        var result = await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        Assert.Equal(AttemptStatus.Invalidated, result.Status);
        Assert.Equal(0, result.Score);
        Assert.Equal(0, result.EarnedXp);
    }

    [Fact]
    public async Task RequireFeedback_ParksPerfectSubmissionForReview()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync(xpReward: 40, requireFeedback: true);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var start = await client.StartAsync(assignment.Id);
        var result = await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        Assert.Equal(AttemptStatus.PendingReview, result.Status); // Despite the perfect score.
        Assert.Equal(40, result.EarnedXp); // Provisional XP.

        var review = await teacher.PostAsJsonAsync($"/api/admin/attempts/{result.AttemptId}/review",
            new { score = 1, feedback = "Well done" }, Json);
        review.EnsureSuccessStatusCode();
        var reviewed = (await review.Content.ReadFromJsonAsync<AttemptAdminDto>(Json))!;

        Assert.Equal(AttemptStatus.Completed, reviewed.Status);
        Assert.Equal(40, reviewed.EarnedXp);
    }

    [Fact]
    public async Task Matching_EarnsPartialCredit()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateMatchingAssignmentAsync(xpReward: 100);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var start = await client.StartAsync(assignment.Id);
        // 3 of 4 correct.
        var result = await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new
        {
            matches = new Dictionary<string, string>
            {
                ["k1"] = "v1", ["k2"] = "v2", ["k3"] = "v3", ["k4"] = "v1"
            }
        });

        Assert.Equal(3, result.Score);
        Assert.Equal(4, result.MaxScore);
        Assert.Equal(75, result.EarnedXp);
    }

    [Fact]
    public async Task OverrideAnswer_RecomputesScoreAndXp()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateMatchingAssignmentAsync(xpReward: 100);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var start = await client.StartAsync(assignment.Id);
        var result = await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new
        {
            matches = new Dictionary<string, string>
            {
                ["k1"] = "v1", ["k2"] = "v2", ["k3"] = "v3", ["k4"] = "wrong"
            }
        });
        Assert.Equal(3, result.Score);

        var questionId = assignment.Questions.Single().Id;
        var overrideResponse = await teacher.PostAsJsonAsync(
            $"/api/admin/attempts/{result.AttemptId}/override-answer",
            new { questionId, points = 4 }, Json);
        overrideResponse.EnsureSuccessStatusCode();
        var overridden = (await overrideResponse.Content.ReadFromJsonAsync<AttemptAdminDto>(Json))!;

        Assert.Equal(4, overridden.Score);
        Assert.Equal(100, overridden.EarnedXp);

        // Points above the question maximum are rejected.
        var tooMany = await teacher.PostAsJsonAsync(
            $"/api/admin/attempts/{result.AttemptId}/override-answer",
            new { questionId, points = 99 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task Dashboard_ShowsActiveAndClosed_NeverDrafts()
    {
        using var teacher = await factory.TeacherClientAsync();
        var draft = await teacher.CreateSingleChoiceAssignmentAsync(activate: false);
        var active = await teacher.CreateSingleChoiceAssignmentAsync();
        var closed = await teacher.CreateSingleChoiceAssignmentAsync();
        await teacher.PostAsJsonAsync($"/api/admin/assignments/{closed.Id}/state", new { state = "Closed" }, Json);

        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);
        var dashboard = (await client.GetFromJsonAsync<List<StudentAssignmentSummaryDto>>("/api/student/assignments", Json))!;

        Assert.DoesNotContain(dashboard, g => g.Id == draft.Id);
        Assert.Equal(AssignmentState.Active, dashboard.Single(g => g.Id == active.Id).State);
        Assert.Equal(AssignmentState.Closed, dashboard.Single(g => g.Id == closed.Id).State);

        // Closed assignments cannot be started.
        var startClosed = await client.PostAsync($"/api/student/assignments/{closed.Id}/start", null);
        Assert.Equal(HttpStatusCode.NotFound, startClosed.StatusCode);
    }
}
