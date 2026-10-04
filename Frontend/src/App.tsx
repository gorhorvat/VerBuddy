import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import ChangePasswordPage from './pages/ChangePasswordPage'
import ResetPasswordPage from './pages/ResetPasswordPage'
import Leaderboard from './pages/Leaderboard'
import StudentDashboard from './pages/student/StudentDashboard'
import TakeAssignment from './pages/student/TakeAssignment'
import AnswerReview from './pages/student/AnswerReview'
import Rewards from './pages/student/Rewards'
import RewardsAdmin from './pages/teacher/RewardsAdmin'
import TeacherAssignments from './pages/teacher/TeacherAssignments'
import AssignmentEditor from './pages/teacher/AssignmentEditor'
import AssignmentAnswers from './pages/teacher/AssignmentAnswers'
import Reviews from './pages/teacher/Reviews'
import Students from './pages/teacher/Students'
import Admins from './pages/superadmin/Admins'

function AppRoutes() {
  const { user, isAdmin, isSuperAdmin } = useAuth()

  if (!user) {
    return (
      <Routes>
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/" element={<LoginPage />} />
        {/* Clean the URL: any deep link without a session lands on "/". */}
        <Route path="*" element={<Navigate to="/" replace />} />
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
        {isAdmin ? (
          <>
            <Route path="/teacher/assignments" element={<TeacherAssignments />} />
            <Route path="/teacher/assignments/:id" element={<AssignmentEditor />} />
            <Route path="/teacher/assignments/:id/answers" element={<AssignmentAnswers />} />
            <Route path="/teacher/reviews" element={<Reviews />} />
            <Route path="/teacher/students" element={<Students />} />
            <Route path="/teacher/rewards" element={<RewardsAdmin />} />
            {isSuperAdmin && <Route path="/superadmin/admins" element={<Admins />} />}
            <Route path="*" element={<Navigate to="/teacher/assignments" replace />} />
          </>
        ) : (
          <>
            <Route path="/assignments" element={<StudentDashboard />} />
            <Route path="/assignments/:id/take" element={<TakeAssignment />} />
            <Route path="/assignments/:id/answers" element={<AnswerReview />} />
            <Route path="/rewards" element={<Rewards />} />
            <Route path="*" element={<Navigate to="/assignments" replace />} />
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
