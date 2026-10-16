using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

public sealed class CatalogService(IContentRepository content)
{
    public IReadOnlyList<Track> Tracks => content.Current.Tracks;

    public IReadOnlyList<Course> GetCourses(string trackId) =>
        content.Current.FindTrack(trackId)?.CourseIds
            .Select(content.Current.FindCourse).OfType<Course>().ToList() ?? [];

    public Course? GetCourse(string courseId) => content.Current.FindCourse(courseId);

    public Lesson? GetLesson(string lessonId) => content.Current.FindLesson(lessonId);

    public IReadOnlyList<Lesson> GetLessons(string courseId) => content.Current.LessonsOf(courseId).ToList();

    public (Lesson Lesson, Exercise Exercise)? FindExercise(string exerciseId) => content.Current.FindExercise(exerciseId);
}
