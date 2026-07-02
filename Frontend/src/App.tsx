import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import ChangePasswordPage from './pages/ChangePasswordPage'
import ResetPasswordPage from './pages/ResetPasswordPage'
import Leaderboard from './pages/Leaderboard'
import StudentDashboard from './pages/student/StudentDashboard'
import PlayGame from './pages/student/PlayGame'
import AnswerReview from './pages/student/AnswerReview'
import TeacherGames from './pages/teacher/TeacherGames'
import GameEditor from './pages/teacher/GameEditor'
import GameAnswers from './pages/teacher/GameAnswers'
import Reviews from './pages/teacher/Reviews'
import Students from './pages/teacher/Students'

function AppRoutes() {
  const { user, isTeacher } = useAuth()

  if (!user) {
    return (
      <Routes>
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="*" element={<LoginPage />} />
      </Routes>
    )
  }

  // First login with an emailed temp password: block the app until changed.
  if (user.mustChangePassword) {
    return <ChangePasswordPage />
  }

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/leaderboard" element={<Leaderboard />} />
        {isTeacher ? (
          <>
            <Route path="/teacher/games" element={<TeacherGames />} />
            <Route path="/teacher/games/:id" element={<GameEditor />} />
            <Route path="/teacher/games/:id/answers" element={<GameAnswers />} />
            <Route path="/teacher/reviews" element={<Reviews />} />
            <Route path="/teacher/students" element={<Students />} />
            <Route path="*" element={<Navigate to="/teacher/games" replace />} />
          </>
        ) : (
          <>
            <Route path="/games" element={<StudentDashboard />} />
            <Route path="/games/:id/play" element={<PlayGame />} />
            <Route path="/games/:id/answers" element={<AnswerReview />} />
            <Route path="*" element={<Navigate to="/games" replace />} />
          </>
        )}
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </AuthProvider>
  )
}
