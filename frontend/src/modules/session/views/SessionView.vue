<script setup lang="ts">
import axios from 'axios'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { UiAlert, UiBasePage, UiButton, UiCard, UiLayout } from '../../../shared/components'
import { getSession, login, logout, type Session } from '../api/session'

const route = useRoute()
const router = useRouter()
const session = ref<Session | null>(null)
const loading = ref(true)
const submitting = ref(false)
const error = ref('')
const sessionUnavailable = ref(false)
const redirecting = ref(false)
const expired = ref(false)
let expirationTimer: ReturnType<typeof setTimeout> | undefined
let requestVersion = 0
let disposed = false

const fullName = computed(() => session.value
  ? [session.value.user.firstNames, session.value.user.paternalLastName, session.value.user.maternalLastName].filter(Boolean).join(' ')
  : '')

function clearSession(isExpired = false) {
  clearTimeout(expirationTimer)
  session.value = null
  expired.value = isExpired
}

async function refreshSession() {
  if (submitting.value) return
  const version = ++requestVersion
  loading.value = true
  error.value = ''
  sessionUnavailable.value = false
  try {
    const result = await getSession()
    if (disposed || version !== requestVersion) return
    const remaining = Date.parse(result.expiresAt) - Date.now()
    if (!Number.isFinite(remaining)) throw new Error('Invalid expiration')
    if (remaining <= 0) {
      clearSession(true)
      return
    }
    session.value = result
    expired.value = false
    clearTimeout(expirationTimer)
    expirationTimer = setTimeout(() => {
      requestVersion++
      clearSession(true)
      loading.value = false
    }, remaining)
  } catch (cause) {
    if (disposed || version !== requestVersion) return
    clearSession()
    if (!axios.isAxiosError(cause) || cause.response?.status !== 401) {
      sessionUnavailable.value = true
      error.value = 'No fue posible consultar la sesión. Inténtalo nuevamente.'
    }
  } finally {
    if (!disposed && version === requestVersion) loading.value = false
  }
}

async function closeSession() {
  if (submitting.value) return
  submitting.value = true
  requestVersion++
  error.value = ''
  try {
    await logout()
    clearSession()
  } catch (cause) {
    if (axios.isAxiosError(cause) && cause.response?.status === 401) clearSession()
    else error.value = 'No fue posible cerrar la sesión. Inténtalo nuevamente.'
  } finally {
    submitting.value = false
  }
}

function startLogin(provider: 'google' | 'microsoft') {
  if (redirecting.value) return
  redirecting.value = true
  login(provider)
}

function onVisibilityChange() {
  if (document.visibilityState === 'visible') void refreshSession()
}

onMounted(() => {
  if (route.query.session === 'error') {
    error.value = 'No fue posible iniciar sesión. Inténtalo nuevamente.'
    void router.replace({ query: { ...route.query, session: undefined } })
  }
  document.addEventListener('visibilitychange', onVisibilityChange)
  // Preserve the callback error while checking whether a session already exists.
  const callbackError = error.value
  void refreshSession().then(() => {
    if (callbackError && !session.value && !error.value) error.value = callbackError
  })
})

onUnmounted(() => {
  disposed = true
  requestVersion++
  clearTimeout(expirationTimer)
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>

<template>
  <UiBasePage title="Inicio" description="Tecnológico Nacional de México · Campus Monclova">
    <UiCard v-if="loading" title="Sesión">
      <p role="status">Consultando tu sesión…</p>
    </UiCard>
    <UiCard v-else-if="session" title="Bienvenida">
      <UiLayout kind="stack">
        <p>Bienvenido, {{ fullName }}.</p>
        <p>Tipo de usuario: <strong>{{ session.user.userType.name }}</strong></p>
        <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
        <div><UiButton variant="secondary" :loading="submitting" @click="closeSession">Cerrar sesión</UiButton></div>
      </UiLayout>
    </UiCard>
    <UiCard v-else title="Iniciar sesión" description="Ingresa con tu cuenta institucional.">
      <UiLayout kind="stack">
        <UiAlert v-if="expired" tone="info">Tu sesión venció. Inicia sesión nuevamente.</UiAlert>
        <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
        <template v-if="sessionUnavailable">
          <div><UiButton @click="refreshSession">Reintentar</UiButton></div>
        </template>
        <template v-else>
          <div><UiButton :disabled="redirecting" @click="startLogin('google')">Continuar con Google</UiButton></div>
          <div><UiButton variant="secondary" :disabled="redirecting" @click="startLogin('microsoft')">Continuar con Microsoft</UiButton></div>
        </template>
      </UiLayout>
    </UiCard>
  </UiBasePage>
</template>
