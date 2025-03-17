let daySchedules=[]

async function downloadAndDisplay() {
    const response = await fetch('http://localhost:5267/scheduler')
    const sched = await response.json()
    console.log(sched)

    document.querySelector('#schedule').innerHTML = ''
    daySchedules = []


    sched.map(x => {
        daySchedules.push(x)

        let tr = document.createElement('tr')
        let tdDay = document.createElement('td')
        let tdTotalMin = document.createElement('td')
        let tdTasks = document.createElement('td')

        tdDay.innerHTML =  x.dayNumber
        tdTotalMin.innerHTML = x.totalMinutes
        
        tr.appendChild(tdDay)
        tr.appendChild(tdTotalMin)

        document.querySelector('#schedule').appendChild(tr)
    })
}


function createTask() {
    let taskname = document.querySelector('#create-task-name').value
    let totalhours = document.querySelector('#create-task-totalhours').value
    let availabledays = document.querySelector('#create-task-availabledays').value
    
    fetch('http://localhost:5267/scheduler', {
        method: 'POST',
        headers: { 'Content-Type' : 'application/json', },
        body: JSON.stringify({
            name: taskname,
            totalHours: Number(totalhours),
            availableDays: Number(availabledays)
        })
    })
    .then(resp => {
        console.log('Response: ', resp)
        if (resp.status === 200) {
            downloadAndDisplay()
        }
    })
    .catch(error => console.log(error))
}

downloadAndDisplay()