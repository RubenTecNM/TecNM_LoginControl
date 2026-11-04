<script setup lang="ts">
import axios from 'axios'
import { ModulesPanel } from '../../modules'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { UiAlert, UiBasePage, UiButton, UiCard, UiLayout } from '../../../shared/components'
import { getSession, login, logout, type Session } from '../api/session'

const route = useRoute()
const router = useRouter()
const session = ref<Session | null>(null)
const sessionVersion = ref(0)
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
    sessionVersion.value++
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
  <UiBasePage fluid class="session-page">
    <div class="session-intro">
      <div class="session-intro__content">
        <span class="session-intro__line" aria-hidden="true"></span>
        <p class="session-eyebrow">Un solo acceso</p>
        <h1>Tu comunidad,<br>en un solo lugar.</h1>
        <p class="session-intro__description">Accede a los espacios y servicios de tu comunidad tecnológica.</p>
      </div>
      <p class="session-intro__signature">Tecnológico Nacional de México</p>
    </div>
    <div class="session-content">
      <UiCard v-if="loading" class="session-card">
        <p role="status">Consultando tu sesión…</p>
      </UiCard>
      <UiCard v-else-if="session" class="session-card session-card--dashboard">
        <UiLayout kind="stack">
          <div>
            <p class="session-eyebrow">Bienvenida</p>
            <h2>Bienvenido, {{ fullName }}.</h2>
            <p class="session-description">Tipo de usuario: <strong>{{ session.user.userType.name }}</strong></p>
          </div>
          <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
          <ModulesPanel :key="sessionVersion" :admin="session.user.userType.code === 'superadmin'" @invalid-session="clearSession(true)" />
          <div><UiButton variant="secondary" :loading="submitting" @click="closeSession">Cerrar sesión</UiButton></div>
        </UiLayout>
      </UiCard>
      <UiCard v-else class="session-card">
        <p class="session-eyebrow">Acceso institucional</p>
        <h2>Inicia sesión</h2>
        <p class="session-description">Selecciona el proveedor con el que deseas continuar.</p>
        <div class="session-actions">
          <UiAlert v-if="expired" tone="info">Tu sesión venció. Inicia sesión nuevamente.</UiAlert>
          <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
          <template v-if="sessionUnavailable">
            <UiButton @click="refreshSession">Reintentar</UiButton>
          </template>
          <template v-else>
            <UiButton class="provider-button" variant="secondary" :disabled="redirecting" @click="startLogin('microsoft')">
              <span class="microsoft-mark" aria-hidden="true"><i></i><i></i><i></i><i></i></span>
              <span>Continuar con Microsoft</span><span class="provider-arrow" aria-hidden="true">→</span>
            </UiButton>
            <UiButton class="provider-button" variant="secondary" :disabled="redirecting" @click="startLogin('google')">
              <span class="google-mark" aria-hidden="true">G</span>
              <span>Continuar con Google</span><span class="provider-arrow" aria-hidden="true">→</span>
            </UiButton>
          </template>
        </div>
        <p class="session-hint">Elige una opción para acceder a tu cuenta.</p>
      </UiCard>
    </div>
  </UiBasePage>
</template>

<style scoped>
.session-page { display: grid; grid-template-columns: 5fr 7fr; min-height: 100%; }
.session-intro { display: flex; flex-direction: column; justify-content: space-between; gap: 48px; padding: clamp(64px, 12vw, 172px) clamp(32px, 5vw, 72px) 64px; background: var(--color-primary-tint); }
.session-intro__line { display: block; width: 42px; height: 4px; margin-bottom: 26px; background: var(--color-accent); }
.session-eyebrow { color: var(--color-primary); font-size: 11px; font-weight: 700; text-transform: uppercase; }
:global([data-theme="dark"] .session-eyebrow) { color: var(--color-info); }
.session-intro h1 { margin-top: 24px; font-size: clamp(32px, 3vw, 42px); }
.session-intro__description { max-width: 400px; margin-top: 24px; font-size: 17px; }
.session-intro__signature { font-size: 11px; font-weight: 600; text-transform: uppercase; }
.session-content { display: flex; align-items: center; justify-content: center; padding: 64px 32px; }
.session-card { width: min(100%, 504px); padding: 48px; }
.session-card--dashboard { width: min(100%, 900px); }
.session-card h2 { margin-top: 24px; font-size: 32px; line-height: 1.2; font-weight: 700; letter-spacing: -.025em; overflow-wrap: anywhere; }
.session-description { margin-top: 16px; font-size: 15px; }
.session-actions { display: grid; gap: 16px; margin-top: 64px; }
.provider-button { display: flex; align-items: center; gap: 22px; width: 100%; min-height: 60px; padding: 16px 24px; text-align: left; font-size: 15px; }
.provider-arrow { margin-left: auto; color: var(--color-text-secondary); font-size: 20px; font-weight: 400; }
.microsoft-mark { display: grid; grid-template-columns: repeat(2, 9px); gap: 1px; flex: none; }
.microsoft-mark i { width: 9px; height: 9px; background: #f35325; }
.microsoft-mark i:nth-child(2) { background: #81bc06; }
.microsoft-mark i:nth-child(3) { background: #05a6f0; }
.microsoft-mark i:nth-child(4) { background: #ffba08; }
.google-mark { width: 19px; flex: none; color: #4285f4; font-size: 24px; font-weight: 700; line-height: 1; }
.session-hint { margin-top: 40px; padding-top: 24px; border-top: 1px solid var(--color-border); font-size: 13px; }
@media (max-width: 800px) {
  .session-page { grid-template-columns: 1fr; }
  .session-intro { padding: 48px 24px; }
  .session-intro__signature { display: none; }
  .session-content { padding: 32px 16px 48px; }
}
@media (max-width: 480px) {
  .session-card { padding: 24px; }
  .session-card h2 { font-size: 28px; }
  .session-actions { margin-top: 40px; }
  .provider-button { gap: 14px; padding-inline: 16px; }
}
</style>
