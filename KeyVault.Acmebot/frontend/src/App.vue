<script setup lang="ts">
import { ref } from 'vue'
import { Certificate } from './types'
import { toUnicode, formatCreatedOn, formatExpiresOn } from './utils'

import AddCertificate from './components/AddCertificate.vue'
import ShowCertificate from './components/ShowCertificate.vue'

const loading = ref(false)
const managedCertificates = ref([] as Certificate[])
const unmanagedCertificates = ref([] as Certificate[])

const openAdd = (): void => {
}

const refresh = (): void => {
}

const openDetails = (certificate: Certificate): void => {
}
</script>

<template>
  <h2 class="title is-4">
    Managed certificates
    <div class="buttons are-small is-inline-block is-pulled-right">
      <a class="button" @click="openAdd">
        <span class="icon">
          <i class="fas fa-plus"></i>
        </span>
        <span>Add</span>
      </a>
      <a class="button" @click="refresh" :class="{ 'is-loading': loading }">
        <span class="icon">
          <i class="fas fa-sync"></i>
        </span>
        <span>Refresh</span>
      </a>
    </div>
  </h2>
  <table class="table is-hoverable is-fullwidth">
    <thead>
      <tr>
        <th>Certificate name</th>
        <th>DNS Names</th>
        <th>Created On</th>
        <th>Expires On</th>
        <th></th>
      </tr>
    </thead>
    <tbody>
      <tr
        v-for="certificate in managedCertificates"
        :class="{ 'has-background-danger-light': certificate.isExpired }"
      >
        <td>{{ certificate.name }}</td>
        <td>
          <div class="tags">
            <span
              v-for="dnsName in certificate.dnsNames"
              class="tag is-light is-medium"
            >{{ toUnicode(dnsName) }}</span>
          </div>
        </td>
        <td>{{ formatCreatedOn(certificate.createdOn) }}</td>
        <td>{{ formatExpiresOn(certificate.expiresOn) }}</td>
        <td>
          <div class="buttons are-small">
            <button class="button is-info" @click="openDetails(certificate)">Details</button>
          </div>
        </td>
      </tr>
    </tbody>
  </table>
  <div class="columns">
    <div class="column is-half">
      <h2 class="title is-4">Unmanaged certificates</h2>
      <table class="table is-hoverable is-fullwidth">
        <thead>
          <tr>
            <th>Certificate name</th>
            <th>DNS Names</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="certificate in unmanagedCertificates">
            <td>{{ certificate.name }}</td>
            <td>
              <div class="tags">
                <span
                  v-for="dnsName in certificate.dnsNames"
                  class="tag is-light is-medium"
                >{{ toUnicode(dnsName) }}</span>
              </div>
            </td>
            <td>
              <div class="buttons are-small">
                <button class="button is-info" @click="openDetails(certificate)">Details</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
  <AddCertificate />
  <ShowCertificate />
</template>
