<script setup lang="ts">
import { onMounted, ref } from 'vue'
import axios from 'axios'
import { modulesApi } from './api/modules'
import { UiAlert, UiButton, UiCard, UiField, UiToggle, UiDialog } from '../../shared/components'

import type { Module, User } from './api/types'
const props = defineProps<{ admin: boolean }>()
const emit = defineEmits<{ invalidSession: [] }>()
const modules = ref<Module[]>([]), catalog = ref<Module[]>([]), users = ref<User[]>([]), assignments = ref<Module[]>([]), moduleUsers = ref<User[]>([])
const busy = ref(false), error = ref(''), notice = ref(''), key = ref(''), search = ref(''), selectedUser = ref(''), selectedModule = ref(''), selected = ref<string[]>([])
const confirmation = ref(false), pending = ref<() => void>(() => {})
function confirmAction(action: () => void) { pending.value = action; confirmation.value = true }
const form = ref({ id: '', name: '', entryUrl: '', isActive: true })
function fail(cause: unknown) { if (axios.isAxiosError(cause) && cause.response?.status === 401) emit('invalidSession'); else error.value = axios.isAxiosError(cause) && cause.response?.status === 403 ? 'Ya no tienes permiso para administrar módulos.' : 'No fue posible completar la operación. Inténtalo nuevamente.' }
async function run(action: () => Promise<void>) { if (busy.value) return; busy.value = true; error.value = ''; notice.value = ''; try { await action() } catch (e) { fail(e) } finally { busy.value = false } }
async function load() { modules.value = await modulesApi.list(); if (props.admin) catalog.value = await modulesApi.catalog() }
function launch(id: string) { void run(async () => { const data = await modulesApi.launch(id); window.location.assign(data.url) }) }
function edit(m: Module) { form.value = { id: m.id, name: m.name, entryUrl: m.entryUrl, isActive: m.isActive }; key.value = '' }
function save() { key.value = ''; void run(async () => { const { id, ...body } = form.value; if (id) await modulesApi.update(id, body); else { const data = await modulesApi.create(body); key.value = data.apiKey } form.value = { id: '', name: '', entryUrl: '', isActive: true }; await load(); notice.value = 'Módulo guardado.' }) }
function rotate(id: string) { confirmAction(() => { key.value = ''; void run(async () => { key.value = (await modulesApi.rotate(id)).apiKey }) }) }
function findUsers() { void run(async () => { users.value = await modulesApi.users(search.value) }) }
function userModules() { assignments.value = []; if (!selectedUser.value) return; void run(async () => { assignments.value = await modulesApi.userModules(selectedUser.value) }) }
function toggleAssignment(m: Module) { void run(async () => { await modulesApi.setUserModule(selectedUser.value, m.id, !m.isActive); assignments.value = await modulesApi.userModules(selectedUser.value) }) }
async function refreshModuleUsers() { moduleUsers.value = await modulesApi.moduleUsers(selectedModule.value) }
function chooseModule() { selected.value = []; moduleUsers.value = []; if (!selectedModule.value) return; void run(refreshModuleUsers) }
function bulk(mode: 'selected' | 'all', isActive: boolean, confirmed = false) { if (mode === 'selected' && !selected.value.length) return; if (mode === 'all' && !confirmed) { confirmAction(() => bulk(mode, isActive, true)); return } void run(async () => { const data = await modulesApi.setModuleUsers(selectedModule.value, mode, mode === 'selected' ? selected.value : [], isActive); notice.value = `Creadas: ${data.created}; cambiadas: ${data.changed}; sin cambios: ${data.unchanged}.`; selected.value = []; await refreshModuleUsers() }) }
function name(u: User) { return [u.firstNames, u.paternalLastName, u.maternalLastName].filter(Boolean).join(' ') }
onMounted(() => void run(load))
</script>
<template>
  <div class="modules-panel">
    <UiAlert v-if="error" tone="danger" role="alert">{{ error }}</UiAlert><UiAlert v-if="notice" tone="info">{{ notice }}</UiAlert>
    <section><h3>Mis módulos</h3><p v-if="!modules.length">No tienes módulos disponibles.</p><ul><li v-for="m in modules" :key="m.id"><UiButton :disabled="busy" @click="launch(m.id)">{{ m.name }}</UiButton></li></ul></section>
    <template v-if="admin">
      <h3>Administración de módulos</h3>
      <UiCard><h4>{{ form.id ? 'Editar módulo' : 'Registrar módulo' }}</h4><form class="form" @submit.prevent="save"><UiField v-model="form.name" label="Nombre" required maxlength="150" /><UiField v-model="form.entryUrl" label="URL de entrada" type="url" required /><UiToggle v-model="form.isActive" label="Módulo activo" /><UiButton :disabled="busy" type="submit">Guardar</UiButton><UiButton v-if="form.id" variant="secondary" type="button" @click="form = { id: '', name: '', entryUrl: '', isActive: true }">Cancelar edición</UiButton></form><UiAlert v-if="key" tone="info">Guarda esta clave ahora; no volverá a mostrarse: <code>{{ key }}</code> <button type="button" @click="key = ''">Cerrar</button></UiAlert></UiCard>
      <ul class="rows"><li v-for="m in catalog" :key="m.id"><strong>{{ m.name }}</strong> · {{ m.isActive ? 'Activo' : 'Inactivo' }}<div class="actions"><UiButton variant="secondary" :disabled="busy" @click="edit(m)">Editar</UiButton><UiButton variant="secondary" :disabled="busy" @click="rotate(m.id)">Reemplazar clave</UiButton></div></li></ul>
      <UiCard><h4>Asignaciones por usuario</h4><form class="actions" @submit.prevent="findUsers"><label>Buscar usuario <input v-model="search"></label><UiButton type="submit" :disabled="busy">Buscar</UiButton></form><label>Usuario <select v-model="selectedUser" :disabled="busy" @change="userModules"><option value="">Selecciona un usuario</option><option v-for="u in users" :key="u.id" :value="u.id">{{ name(u) }} — {{ u.googleEmail || u.microsoftEmail }}</option></select></label><ul class="rows"><li v-for="m in assignments" :key="m.id">{{ m.name }} <span v-if="m.moduleActive === false">(módulo inactivo)</span><UiButton variant="secondary" :disabled="busy" @click="toggleAssignment(m)">{{ m.isActive ? 'Desactivar acceso' : 'Activar acceso' }}</UiButton></li></ul></UiCard>
      <UiCard><h4>Asignaciones por módulo</h4><label>Módulo <select v-model="selectedModule" :disabled="busy" @change="chooseModule"><option value="">Selecciona un módulo</option><option v-for="m in catalog" :key="m.id" :value="m.id">{{ m.name }}</option></select></label><template v-if="selectedModule"><label>Buscar en la lista <input v-model="search"></label><ul class="rows"><li v-for="u in moduleUsers.filter(u => `${name(u)} ${u.googleEmail || ''} ${u.microsoftEmail || ''}`.toLowerCase().includes(search.toLowerCase()))" :key="u.id"><label><input v-model="selected" type="checkbox" :value="u.id"> {{ name(u) }} — {{ u.googleEmail || u.microsoftEmail }} ({{ u.isActive ? 'activo' : 'inactivo' }})</label></li></ul><div class="actions"><UiButton :disabled="busy || !selected.length" @click="bulk('selected', true)">Activar seleccionados</UiButton><UiButton variant="secondary" :disabled="busy || !selected.length" @click="bulk('selected', false)">Desactivar seleccionados</UiButton><UiButton variant="secondary" :disabled="busy" @click="bulk('all', true)">Activar todos los existentes</UiButton><UiButton variant="secondary" :disabled="busy" @click="bulk('all', false)">Desactivar todos los existentes</UiButton></div></template></UiCard>
    </template>
    <UiDialog v-model="confirmation" title="Confirmar operación"><p>Confirma que deseas continuar con esta operación.</p><template #actions><UiButton @click="confirmation = false; pending()">Confirmar</UiButton><UiButton variant="secondary" @click="confirmation = false">Cancelar</UiButton></template></UiDialog>
  </div>
</template>
<style scoped>
.modules-panel,.form { display:grid; gap:1rem } h3,h4 { margin-bottom:.75rem } ul { list-style:none; padding:0 } .rows { display:grid; gap:.75rem; margin:1rem 0 } .rows li { border-bottom:1px solid var(--color-border); padding-bottom:.75rem; overflow-wrap:anywhere } .actions { display:flex; flex-wrap:wrap; gap:.5rem; align-items:end } label { display:block; margin:.5rem 0 } input:not([type=checkbox]),select { display:block; width:100%; max-width:100%; padding:.6rem; background:var(--color-surface-white); color:var(--color-text-primary); border:1px solid var(--color-border-medium); border-radius:4px } code { overflow-wrap:anywhere }
</style>
